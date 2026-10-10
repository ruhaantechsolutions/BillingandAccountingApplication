using RBiz.Domain.Common;

namespace RBiz.Domain.Entities;

/// <summary>
/// An isolated customer of the system. Every user and every audit record belongs to
/// exactly one tenant, which is how data is kept separate between businesses.
/// </summary>
public class Tenant : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Display name of the business, e.g. "Ruhaan Tech Solutions".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Short unique code used for lookups and login, e.g. "RUHAAN".</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Soft-disable switch. Users of an inactive tenant cannot log in.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Users belonging to this tenant.</summary>
    public ICollection<User> Users { get; set; } = [];
}
