using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RBiz.Application.Sync;
using RBiz.Application.Sync.Dtos;
using RBiz.Application.Sync.Interfaces;
using RBiz.Domain.Sync;

namespace RBiz.Infrastructure.Persistence.Sync;

/// <summary>
/// Carries the whole of the delta, tombstone and conflict-detection logic so that a new
/// synchronised entity only has to say how its rows map to and from a client payload.
///
/// A subclass supplies: the entity name, the tenant scoping, a to-payload projection and
/// a from-payload apply step. Everything correctness-critical lives here, once.
/// </summary>
internal abstract class SyncEntityProviderBase<TEntity> : ISyncEntityProvider
    where TEntity : SyncableEntity, new()
{
    protected SyncEntityProviderBase(AppDbContext context, TimeProvider timeProvider)
    {
        Context = context;
        TimeProvider = timeProvider;
    }

    protected AppDbContext Context { get; }

    protected TimeProvider TimeProvider { get; }

    public abstract string EntityName { get; }

    /// <summary>
    /// Restricts a write-path lookup to one tenant.
    /// </summary>
    protected abstract IQueryable<TEntity> ForTenant(IQueryable<TEntity> source, Guid tenantId);

    /// <summary>
    /// CLR property holding the tenant key, used to resolve the real column name for the
    /// delta statement. Declared next to <see cref="ForTenant"/> rather than derived from
    /// it by reflection, because two obvious lines beat one clever one.
    /// </summary>
    protected abstract string TenantPropertyName { get; }

    /// <summary>Shapes a row for the client. Must never include a secret.</summary>
    protected abstract object ToPayload(TEntity entity);

    /// <summary>
    /// Assigns the tenant on a newly created row. Kept out of <see cref="ApplyPayload"/>
    /// so a client can never set the tenant itself.
    /// </summary>
    protected abstract void AssignTenant(TEntity entity, Guid tenantId);

    /// <summary>
    /// Copies the client's fields onto the entity, validating as it goes. Returning a
    /// message rejects the change; returning null accepts it.
    ///
    /// Identity, tenant, audit and sync columns are handled by the caller, so an
    /// implementation deals only with its own business fields.
    /// </summary>
    protected abstract string? ApplyPayload(
        TEntity entity,
        Dictionary<string, object?> payload,
        bool isNew);

    public async Task<SyncEntityChangeSet> GetChangesAsync(
        Guid tenantId,
        byte[] afterRowVersion,
        byte[] ceilingRowVersion,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // The range predicate is SQL rather than LINQ because a rowversion is binary(8)
        // and C# has no comparison operator for byte[] - there would be nothing for EF
        // Core to translate. SQL Server compares the binary values directly, which is
        // exactly the ordering a rowversion is defined by.
        //
        // Identifiers come from the EF model, never from a caller, so there is no
        // injection surface. The three values are passed as parameters.
        var sql = BuildDeltaSql(limit + 1);

        // IgnoreQueryFilters is essential: the soft-delete filter would otherwise hide
        // exactly the tombstones the client needs in order to drop its local copy.
        //
        // Keyset range on the row version - no OFFSET, so the thousandth page costs the
        // same as the first, served off the (IsDeleted, RowVersion) index. One extra row
        // is fetched purely to detect another page, avoiding a second COUNT.
        var rows = await Context.Set<TEntity>()
            .FromSqlRaw(sql, tenantId, afterRowVersion, ceilingRowVersion)
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > limit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var changes = new SyncEntityChange[rows.Count];

        for (var i = 0; i < rows.Count; i++)
        {
            var entity = rows[i];

            changes[i] = new SyncEntityChange(
                entity.Id,
                entity.IsDeleted ? SyncChangeType.Delete : SyncChangeType.Upsert,
                SyncCursor.Encode(entity.RowVersion),
                entity.IsDeleted ? entity.DeletedAtUtc : entity.ModifiedAtUtc ?? entity.CreatedAtUtc,
                // A tombstone carries no body: the client only needs to know it is gone.
                entity.IsDeleted ? null : ToPayload(entity));
        }

        // Resume from the last row actually returned, or from the ceiling when this
        // entity had nothing - which lets an idle entity's cursor keep up instead of
        // rescanning the same empty range on every sync.
        var nextCursor = rows.Count > 0
            ? SyncCursor.Encode(rows[^1].RowVersion)
            : SyncCursor.Encode(ceilingRowVersion);

        return new SyncEntityChangeSet(EntityName, changes, nextCursor, hasMore);
    }

    public async Task<SyncPushItemResult> ApplyAsync(
        Guid tenantId,
        SyncPushItem item,
        CancellationToken cancellationToken = default)
    {
        if (item.Id == Guid.Empty)
        {
            return Rejected(item, "id is required.");
        }

        // Tracked, and tenant-scoped: a client cannot reach another tenant's row even by
        // guessing its id, because the predicate would not match.
        var existing = await ForTenant(Context.Set<TEntity>().IgnoreQueryFilters(), tenantId)
            .FirstOrDefaultAsync(e => e.Id == item.Id, cancellationToken);

        return item.ChangeType switch
        {
            SyncChangeType.Delete => ApplyDelete(item, existing),
            SyncChangeType.Upsert => ApplyUpsert(tenantId, item, existing),
            _ => Rejected(item, $"Unsupported change type '{item.ChangeType}'.")
        };
    }

    private SyncPushItemResult ApplyUpsert(Guid tenantId, SyncPushItem item, TEntity? existing)
    {
        if (item.Payload is null || item.Payload.Count == 0)
        {
            return Rejected(item, "payload is required for an upsert.");
        }

        var isNew = existing is null;

        if (isNew)
        {
            // A client claiming to update a row the server does not have is a
            // disagreement about state - reported, never guessed at.
            if (!string.IsNullOrEmpty(item.BaseRowVersion))
            {
                return Conflict(item, null, "The row does not exist on the server.");
            }
        }
        else
        {
            var conflict = CheckVersion(item, existing!);

            if (conflict is not null)
            {
                return conflict;
            }

            if (existing!.IsDeleted)
            {
                return Conflict(item, existing, "The row was deleted on the server.");
            }
        }

        var entity = existing ?? new TEntity { Id = item.Id };

        if (isNew)
        {
            AssignTenant(entity, tenantId);
        }

        var rejection = ApplyPayload(entity, item.Payload, isNew);

        if (rejection is not null)
        {
            return Rejected(item, rejection);
        }

        if (isNew)
        {
            Context.Set<TEntity>().Add(entity);
        }

        // RowVersion is not reported back: the database assigns the new value during the
        // batch commit, after this method returns. The client picks it up on its next pull.
        return Applied(item, null, null);
    }

    private SyncPushItemResult ApplyDelete(SyncPushItem item, TEntity? existing)
    {
        // Deleting something absent or already gone is success. A client retrying a
        // delete it already completed should be able to move on.
        if (existing is null || existing.IsDeleted)
        {
            return Applied(
                item,
                SyncCursor.Encode(existing?.RowVersion),
                "Already deleted on the server.");
        }

        var conflict = CheckVersion(item, existing);

        if (conflict is not null)
        {
            return conflict;
        }

        // Remove() is converted into a tombstone by AppDbContext, so the deletion still
        // reaches every other device.
        Context.Set<TEntity>().Remove(existing);

        return Applied(item, null, null);
    }

    /// <summary>
    /// Compares the version the client last saw against the server's current one. This is
    /// the whole of conflict detection: unequal means somebody else got there first.
    /// </summary>
    private SyncPushItemResult? CheckVersion(SyncPushItem item, TEntity existing)
    {
        if (string.IsNullOrEmpty(item.BaseRowVersion))
        {
            return Conflict(item, existing, "The row already exists on the server.");
        }

        if (!SyncCursor.TryDecode(item.BaseRowVersion, out var clientVersion))
        {
            return Rejected(item, "baseRowVersion is not a valid row version.");
        }

        return SyncCursor.Compare(clientVersion, existing.RowVersion) == 0
            ? null
            : Conflict(
                item,
                existing,
                "The row was changed on the server since this device last saw it.");
    }

    /// <summary>
    /// Builds the delta statement from the EF model, so table and column names always
    /// match the real schema even after a rename.
    /// </summary>
    private string BuildDeltaSql(int take)
    {
        var entityType = Context.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException(
                $"{typeof(TEntity).Name} is not part of the EF Core model.");

        var tableName = entityType.GetTableName()
            ?? throw new InvalidOperationException(
                $"{typeof(TEntity).Name} is not mapped to a table.");

        var schema = entityType.GetSchema();

        var qualifiedTable = schema is null
            ? Quote(tableName)
            : Quote(schema) + "." + Quote(tableName);

        var tenantColumn = Quote(ColumnName(entityType, TenantPropertyName));
        var rowVersion = Quote(ColumnName(entityType, nameof(SyncableEntity.RowVersion)));

        return
            $"SELECT TOP({take}) * FROM {qualifiedTable} " +
            $"WHERE {tenantColumn} = {{0}} " +
            $"AND {rowVersion} > {{1}} " +
            $"AND {rowVersion} <= {{2}} " +
            $"ORDER BY {rowVersion}";
    }

    private static string ColumnName(IEntityType entityType, string propertyName)
    {
        var property = entityType.FindProperty(propertyName)
            ?? throw new InvalidOperationException(
                $"{entityType.ClrType.Name} has no mapped property '{propertyName}'.");

        return property.GetColumnName();
    }

    /// <summary>
    /// Brackets an identifier, doubling any closing bracket inside it. The names come
    /// from the EF model, but quoting keeps the statement correct for a column that
    /// needs it.
    /// </summary>
    private static string Quote(string identifier) =>
        "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";

    private SyncPushItemResult Applied(SyncPushItem item, string? rowVersion, string? message) =>
        new(item.OperationId, item.Id, EntityName, SyncResultStatus.Applied, rowVersion, message, null);

    /// <summary>
    /// Returns the server's current row with a conflict, so the client can merge without
    /// a second round trip.
    /// </summary>
    private SyncPushItemResult Conflict(SyncPushItem item, TEntity? serverRow, string message) => new(
        item.OperationId,
        item.Id,
        EntityName,
        SyncResultStatus.Conflict,
        SyncCursor.Encode(serverRow?.RowVersion),
        message,
        serverRow is null || serverRow.IsDeleted ? null : ToPayload(serverRow));

    private SyncPushItemResult Rejected(SyncPushItem item, string message) =>
        new(item.OperationId, item.Id, EntityName, SyncResultStatus.Rejected, null, message, null);
}
