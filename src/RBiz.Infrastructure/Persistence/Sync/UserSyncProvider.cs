using RBiz.Domain.Entities;

namespace RBiz.Infrastructure.Persistence.Sync;

/// <summary>
/// Sync support for <see cref="User"/>.
///
/// This is the shape every future entity follows: name it, scope it to the tenant, say
/// what a client may read, and say what a client may write. No delta, cursor, tombstone
/// or conflict code is repeated.
/// </summary>
internal sealed class UserSyncProvider : SyncEntityProviderBase<User>
{
    public UserSyncProvider(AppDbContext context, TimeProvider timeProvider)
        : base(context, timeProvider)
    {
    }

    public override string EntityName => "users";

    protected override string TenantPropertyName => nameof(User.TenantId);

    protected override IQueryable<User> ForTenant(IQueryable<User> source, Guid tenantId) =>
        source.Where(u => u.TenantId == tenantId);

    protected override void AssignTenant(User entity, Guid tenantId) => entity.TenantId = tenantId;

    /// <summary>
    /// Note what is absent: PasswordHash. A credential must never be replicated onto a
    /// device, and an offline cache is exactly the kind of place it would be forgotten.
    /// </summary>
    protected override object ToPayload(User entity) => new
    {
        entity.Id,
        entity.TenantId,
        entity.Email,
        entity.FullName,
        entity.IsActive,
        entity.CreatedAtUtc,
        entity.ModifiedAtUtc
    };

    /// <summary>
    /// A device may edit a user's display name and active flag. It may not change an
    /// email (that is the login identity, and uniqueness has to be settled server-side)
    /// and it certainly may not set a password hash.
    /// </summary>
    protected override string? ApplyPayload(User entity, Dictionary<string, object?> payload, bool isNew)
    {
        if (isNew)
        {
            // Creating a user requires a password, which this endpoint deliberately does
            // not accept. Users are provisioned online through the Users endpoint.
            return "New users cannot be created through sync. Use POST /api/v1/Users while online.";
        }

        if (payload.TryGetValue("fullName", out var fullNameValue))
        {
            // Read through the helper: a JsonElement's ToString() for a string includes
            // the surrounding quotes, so naive conversion would store "\"Name\"".
            if (!SyncPayload.TryReadString(fullNameValue, out var fullName))
            {
                return "fullName cannot be empty.";
            }

            entity.FullName = fullName;
        }

        if (payload.TryGetValue("isActive", out var isActiveValue))
        {
            if (!SyncPayload.TryReadBoolean(isActiveValue, out var isActive))
            {
                return "isActive must be true or false.";
            }

            entity.IsActive = isActive;
        }

        return null;
    }
}
