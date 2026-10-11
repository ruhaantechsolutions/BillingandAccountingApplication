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

    /// <summary>
    /// Empty: creating or renaming a business is an administrative act that belongs
    /// online, where the uniqueness of its code can be enforced properly. Any payload
    /// sent for a tenant is rejected with that explanation.
    /// </summary>
    private static readonly HashSet<string> Writable = new(StringComparer.OrdinalIgnoreCase);

    public override string EntityName => "tenants";

    protected override IReadOnlySet<string> WritableFields => Writable;

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
    /// Unreachable in practice: an empty <see cref="WritableFields"/> means the base class
    /// rejects every payload before it gets here, and an empty payload is rejected earlier
    /// still. Kept as a defence so the entity can never become writable by accident.
    /// </summary>
    protected override string? ApplyPayload(Tenant entity, Dictionary<string, object?> payload, bool isNew) =>
        "Tenants are read-only over sync. Use the Tenants endpoints while online.";
}
