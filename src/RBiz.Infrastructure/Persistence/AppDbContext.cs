using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RBiz.Application.Interfaces;
using RBiz.Domain.Common;
using RBiz.Domain.Entities;

namespace RBiz.Infrastructure.Persistence;

/// <summary>
/// The EF Core unit of work for the application. Beyond mapping the tables it also
/// stamps the audit columns and writes the audit trail on every save, so no service can
/// forget to do it and no service needs to know it happens.
/// </summary>
public sealed class AppDbContext : DbContext, IUnitOfWork
{
    /// <summary>
    /// Property names that must never be copied into the audit trail. A password hash in
    /// an audit row would be a second copy of the credential in a table that is, by
    /// design, widely readable and never deleted.
    /// </summary>
    private static readonly HashSet<string> RedactedProperties =
        new(StringComparer.OrdinalIgnoreCase) { nameof(User.PasswordHash) };

    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
        : base(options)
    {
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<User> Users => Set<User>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up every IEntityTypeConfiguration in this assembly, so a new entity only
        // needs its own configuration class and nothing added here.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var userId = _currentUser.UserId;

        StampAuditableEntities(nowUtc, userId);

        // Built before saving, while the change tracker still knows the original values.
        var auditLogs = BuildAuditLogs(nowUtc, userId);

        if (auditLogs.Count > 0)
        {
            AuditLogs.AddRange(auditLogs);
        }

        // One round trip commits the business change and its audit rows together, so the
        // trail can never disagree with the data.
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Fills in CreatedAt/CreatedBy on insert and ModifiedAt/ModifiedBy on update.
    /// </summary>
    private void StampAuditableEntities(DateTime nowUtc, Guid? userId)
    {
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = nowUtc;
                    entry.Entity.CreatedBy = userId;
                    break;

                case EntityState.Modified:
                    entry.Entity.ModifiedAtUtc = nowUtc;
                    entry.Entity.ModifiedBy = userId;

                    // Defend the original values against a caller that posted them back.
                    entry.Property(e => e.CreatedAtUtc).IsModified = false;
                    entry.Property(e => e.CreatedBy).IsModified = false;
                    break;
            }
        }
    }

    /// <summary>
    /// Turns the pending changes into audit rows, recording only the columns that actually
    /// changed rather than the whole entity.
    /// </summary>
    private List<AuditLog> BuildAuditLogs(DateTime nowUtc, Guid? userId)
    {
        var logs = new List<AuditLog>();
        var tenantId = _currentUser.TenantId;

        foreach (var entry in ChangeTracker.Entries())
        {
            // Never audit the audit trail, and ignore entities that are merely loaded.
            if (entry.Entity is AuditLog ||
                entry.State is EntityState.Detached or EntityState.Unchanged)
            {
                continue;
            }

            var action = entry.State switch
            {
                EntityState.Added => AuditAction.Created,
                EntityState.Deleted => AuditAction.Deleted,
                _ => AuditAction.Updated
            };

            var (oldValues, newValues) = CaptureValues(entry, action);

            logs.Add(new AuditLog
            {
                TenantId = ResolveTenantId(entry) ?? tenantId,
                UserId = userId,
                Action = action,
                EntityName = entry.Metadata.ClrType.Name,
                EntityId = ResolvePrimaryKey(entry),
                OldValues = oldValues,
                NewValues = newValues,
                TimestampUtc = nowUtc
            });
        }

        return logs;
    }

    /// <summary>
    /// Serialises the before and after state of the changed columns to JSON.
    /// </summary>
    private static (string? OldValues, string? NewValues) CaptureValues(
        EntityEntry entry,
        AuditAction action)
    {
        Dictionary<string, object?>? oldValues = null;
        Dictionary<string, object?>? newValues = null;

        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;

            if (RedactedProperties.Contains(name))
            {
                continue;
            }

            switch (action)
            {
                case AuditAction.Created:
                    (newValues ??= [])[name] = property.CurrentValue;
                    break;

                case AuditAction.Deleted:
                    (oldValues ??= [])[name] = property.OriginalValue;
                    break;

                // On update, record only what moved. A row with forty unchanged columns
                // should not produce forty entries of noise.
                case AuditAction.Updated when property.IsModified:
                    (oldValues ??= [])[name] = property.OriginalValue;
                    (newValues ??= [])[name] = property.CurrentValue;
                    break;
            }
        }

        return (Serialize(oldValues), Serialize(newValues));
    }

    private static string? Serialize(Dictionary<string, object?>? values) =>
        values is null ? null : JsonSerializer.Serialize(values);

    /// <summary>
    /// Reads the TenantId off the entity when it has one, so an audit row is attributed to
    /// the right tenant even for a system-initiated change.
    /// </summary>
    private static Guid? ResolveTenantId(EntityEntry entry) => entry.Entity switch
    {
        User user => user.TenantId,
        Tenant tenant => tenant.Id,
        _ => null
    };

    /// <summary>
    /// Formats the primary key as text, joining the parts of a composite key.
    /// </summary>
    private static string ResolvePrimaryKey(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();

        if (key is null)
        {
            return string.Empty;
        }

        var parts = key.Properties
            .Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? string.Empty);

        return string.Join('|', parts);
    }
}
