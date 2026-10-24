using RBiz.Domain.Sync;

namespace RBiz.Domain.Entities;

/// <summary>
/// How a registration is taxed. A composition taxpayer issues a bill of supply rather
/// than charging GST on a tax invoice, so billing cannot treat every registration alike.
/// </summary>
public enum GstRegistrationType
{
    Regular = 1,
    Composition = 2,
    Other = 3
}

/// <summary>
/// How far a GSTIN has been checked.
///
/// The distinction that matters: a number can be well-formed yet unconfirmed against the
/// live register. Reporting "we could not check" as "invalid" would block a legitimate
/// business from onboarding whenever the verification service is down.
/// </summary>
public enum GstinVerificationStatus
{
    /// <summary>Format, state code and check digit all pass; not checked against the register.</summary>
    FormatValid = 1,

    /// <summary>Confirmed against the GST register.</summary>
    Verified = 2,

    /// <summary>The register could not be reached. The number is not in doubt; the check is.</summary>
    VerificationUnavailable = 3,

    /// <summary>The register replied that this number is not a live registration.</summary>
    NotFound = 4
}

/// <summary>
/// A company's GST registration, kept apart from the company record so that a change of
/// GST status is a new fact with its own effective date and audit trail rather than an
/// overwrite of the company row.
/// </summary>
public class CompanyGstRegistration : SyncableEntity
{
    public Guid CompanyId { get; set; }

    /// <summary>Fifteen characters, stored upper-cased.</summary>
    public string Gstin { get; set; } = string.Empty;

    public GstRegistrationType RegistrationType { get; set; } = GstRegistrationType.Regular;

    /// <summary>When the registration took effect. Optional; not asked for at onboarding.</summary>
    public DateOnly? EffectiveDate { get; set; }

    public GstinVerificationStatus VerificationStatus { get; set; } = GstinVerificationStatus.FormatValid;

    /// <summary>When the register was last consulted, so a stale check is visible as stale.</summary>
    public DateTime? VerifiedAtUtc { get; set; }

    public Company? Company { get; set; }
}
