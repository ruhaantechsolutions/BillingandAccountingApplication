using RBiz.Domain.Sync;

namespace RBiz.Domain.Entities;

/// <summary>
/// A person who can authenticate against the API. Scoped to a single tenant.
/// </summary>
public class User : SyncableEntity
{
    /// <summary>Owning tenant. Part of the uniqueness rule on <see cref="Email"/>.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Login identifier. Unique per tenant, stored normalised to upper case.</summary>
    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    /// <summary>Hashed password. The plain-text password is never stored or logged.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Soft-disable switch. An inactive user cannot log in.</summary>
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
}
