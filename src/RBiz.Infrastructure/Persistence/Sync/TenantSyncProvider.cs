using RBiz.Domain.Entities;

namespace RBiz.Infrastructure.Persistence.Sync;

/// <summary>
/// Sync support for <see cref="Tenant"/>, so a device knows the business it belongs to
/// while offline (name on an invoice header, for instance).
///
/// A device only ever receives its own tenant: the tenant key for this entity is its
/// own primary key.
/// </summary>
internal sealed class TenantSyncProvider : SyncEntityProviderBase<Tenant>
{
    public TenantSyncProvider(AppDbContext context, TimeProvider timeProvider)
        : base(context, timeProvider)
    {
    }

    public override string EntityName => "tenants";

    protected override string TenantPropertyName => nameof(Tenant.Id);

    protected override IQueryable<Tenant> ForTenant(IQueryable<Tenant> source, Guid tenantId) =>
        source.Where(t => t.Id == tenantId);

    protected override void AssignTenant(Tenant entity, Guid tenantId) => entity.Id = tenantId;

    protected override object ToPayload(Tenant entity) => new
    {
        entity.Id,
        entity.Name,
        entity.Code,
        entity.IsActive,
        entity.CreatedAtUtc,
        entity.ModifiedAtUtc
    };

    /// <summary>
    /// Read-only over sync. Creating or renaming a business is an administrative act that
    /// belongs online, where uniqueness of the code can be enforced properly.
    /// </summary>
    protected override string? ApplyPayload(Tenant entity, Dictionary<string, object?> payload, bool isNew) =>
        "Tenants are read-only over sync. Use the Tenants endpoints while online.";
}
