using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RBiz.Domain.Sync;

namespace RBiz.Infrastructure.Persistence.Sync;

/// <summary>
/// The one call that makes an entity synchronisable at the database level.
///
/// When a new entity joins sync later (Billing, Stock, ...) its EF configuration calls
/// ConfigureSyncable and gets the row version, the tombstone columns, the delta index and
/// the soft-delete filter in one line - no chance of forgetting one of them.
/// </summary>
internal static class SyncableEntityConfigurationExtensions
{
    public static EntityTypeBuilder<TEntity> ConfigureSyncable<TEntity>(
        this EntityTypeBuilder<TEntity> builder)
        where TEntity : SyncableEntity
    {
        // A SQL Server rowversion: assigned by the engine on every insert and update,
        // strictly increasing database-wide. It serves as both the change marker the
        // delta query ranges over and the concurrency token EF checks on update.
        builder.Property(e => e.RowVersion)
            .IsRowVersion()
            .HasColumnName("RowVersion");

        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false);

        // Covers the delta query: "rows of this tenant changed after X, in order".
        // Ordering by RowVersion is a total order because the values are unique, which is
        // what allows keyset paging with no OFFSET.
        builder.HasIndex(e => new { e.IsDeleted, e.RowVersion })
            .HasDatabaseName($"IX_{typeof(TEntity).Name}_Sync_RowVersion");

        // Ordinary queries never see tombstones. The sync delta deliberately bypasses
        // this with IgnoreQueryFilters, because a client must be told about deletions.
        builder.HasQueryFilter(e => !e.IsDeleted);

        return builder;
    }
}
