using RBiz.Domain.Sync;

namespace RBiz.Domain.Entities;

/// <summary>Whether a company charges GST.</summary>
public enum GstRegistrationStatus
{
    /// <summary>No GSTIN. Invoices carry no GST by default.</summary>
    NotRegistered = 0,

    /// <summary>Holds a GSTIN; see <see cref="CompanyGstRegistration"/> for the details.</summary>
    Registered = 1
}

/// <summary>
/// A business with its own books. Everything financial - invoices, ledgers, customers,
/// stock - belongs to exactly one company and is never mixed across companies.
///
/// One person may own or work on several companies, which is what
/// <see cref="UserCompany"/> expresses. The company a request is acting on is carried in
/// the access token, so switching company changes the data a caller can reach rather than
/// only the name shown on screen.
/// </summary>
public class Company : SyncableEntity
{
    /// <summary>Business name as the owner wants it shown. Not required to be unique.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Kept on the company as well as on the registration row so that "does this company
    /// charge GST" is answerable without a join, which it is on nearly every billing path.
    /// </summary>
    public GstRegistrationStatus GstStatus { get; set; } = GstRegistrationStatus.NotRegistered;

    /// <summary>Soft-disable switch for the whole business.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The client-supplied id of the request that created this company, unique per user.
    ///
    /// A double-clicked button or a retried call after a dropped connection arrives as the
    /// same request id, and returns this company instead of creating a second set of books
    /// for the same business.
    /// </summary>
    public Guid CreationRequestId { get; set; }

    /// <summary>The company's own address. Never shared with another company.</summary>
    public CompanyAddress? Address { get; set; }

    /// <summary>Present only while <see cref="GstStatus"/> is Registered.</summary>
    public CompanyGstRegistration? GstRegistration { get; set; }

    public ICollection<UserCompany> Members { get; set; } = [];

    public ICollection<Location> Locations { get; set; } = [];
}
