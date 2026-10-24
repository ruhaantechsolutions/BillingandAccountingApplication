using RBiz.Domain.Sync;

namespace RBiz.Domain.Entities;

/// <summary>
/// A company's own address.
///
/// When a second company is created the user may copy an address from one they already
/// have. That copies the values into a new row; it does not share one. Editing the source
/// company's address later must never change this one, which is why
/// <see cref="CopiedFromCompanyId"/> records only where the values came from and carries
/// no live relationship.
/// </summary>
public class CompanyAddress : SyncableEntity
{
    public Guid CompanyId { get; set; }

    public string Line1 { get; set; } = string.Empty;

    public string? Line2 { get; set; }

    public string City { get; set; } = string.Empty;

    /// <summary>Canonical state or union territory name. See IndianStates.</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>Six digits, never starting with zero.</summary>
    public string PinCode { get; set; } = string.Empty;

    /// <summary>Fixed to India for now; stored so the column is already there when it is not.</summary>
    public string Country { get; set; } = "India";

    /// <summary>
    /// Audit metadata only: which company these values were copied from at creation time.
    /// Not a relationship, and never used to read the other company's current address.
    /// </summary>
    public Guid? CopiedFromCompanyId { get; set; }

    public Company? Company { get; set; }
}
