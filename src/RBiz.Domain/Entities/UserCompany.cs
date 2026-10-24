using RBiz.Domain.Common;

namespace RBiz.Domain.Entities;

/// <summary>What a person may do inside one company.</summary>
public enum CompanyRole
{
    /// <summary>Created the company. Full control, including membership.</summary>
    Owner = 1,

    /// <summary>Full operational control, granted by an owner.</summary>
    Admin = 2,

    /// <summary>Day-to-day use.</summary>
    Member = 3
}

/// <summary>
/// Links a person to a company and records what they may do there.
///
/// This is what allows one login to hold several businesses, and an accountant to work on
/// companies belonging to different owners - neither of which a single foreign key on the
/// user could express.
///
/// Deliberately NOT synchronised: permissions decide what a device is allowed to see, so
/// they are answered by the server on every request and never cached on the device.
/// </summary>
public class UserCompany : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid CompanyId { get; set; }

    public CompanyRole Role { get; set; } = CompanyRole.Member;

    /// <summary>
    /// Revokes access without deleting the row, so the audit trail still shows that this
    /// person once had access and when it ended.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public User? User { get; set; }

    public Company? Company { get; set; }
}
