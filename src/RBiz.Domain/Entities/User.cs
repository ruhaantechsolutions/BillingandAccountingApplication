using RBiz.Domain.Sync;

namespace RBiz.Domain.Entities;

/// <summary>
/// A person who signs in. Independent of any company: the companies a person can reach
/// are the ones <see cref="UserCompany"/> links them to, which may be none at first, and
/// may belong to different owners.
/// </summary>
public class User : SyncableEntity
{
    /// <summary>
    /// Ten digits, the login identity, unique across the whole system.
    ///
    /// Global rather than per-company because sign-in asks only for a mobile number and a
    /// password - there is no company context yet at that point to scope it by.
    /// </summary>
    public string Mobile { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Optional. Not used to sign in; kept for notifications and account recovery. Unique
    /// where present, so two accounts cannot claim the same address.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>Hashed password. The plain-text password is never stored or logged.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Soft-disable switch. An inactive user cannot sign in.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>The companies this person can work on.</summary>
    public ICollection<UserCompany> Companies { get; set; } = [];
}
