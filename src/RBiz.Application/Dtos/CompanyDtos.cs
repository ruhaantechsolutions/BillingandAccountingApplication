using RBiz.Domain.Entities;

namespace RBiz.Application.Dtos;

/// <summary>Where a new company's address comes from.</summary>
public enum AddressSource
{
    /// <summary>The caller supplies the address fields.</summary>
    Manual = 1,

    /// <summary>Copy the values from a company the caller already has.</summary>
    CopyFromCompany = 2
}

/// <summary>An address as entered or returned.</summary>
public sealed record AddressDto(
    string Line1,
    string? Line2,
    string City,
    string State,
    string PinCode,
    string Country);

/// <summary>
/// A company in a list: enough for the switcher and the "existing companies" panel,
/// without loading everything about each one.
/// </summary>
public sealed record CompanySummaryDto(
    Guid Id,
    string Name,
    string City,
    string State,
    GstRegistrationStatus GstStatus,
    CompanyRole Role);

/// <summary>A company with everything the dashboard needs after creation.</summary>
public sealed record CompanyDto(
    Guid Id,
    string Name,
    GstRegistrationStatus GstStatus,
    bool IsActive,
    AddressDto Address,
    string? Gstin,
    GstRegistrationType? GstRegistrationType,
    GstinVerificationStatus? GstVerificationStatus,
    CompanyRole Role,
    DateTime CreatedAtUtc);

/// <summary>
/// Everything the Create Company screen collects.
/// </summary>
/// <param name="Name">Business name. Required.</param>
/// <param name="AddressSource">Whether the address is typed or copied.</param>
/// <param name="CopyAddressFromCompanyId">
/// Required when copying. Must be a company the caller can already reach - checked on the
/// server, never taken on trust from the dropdown the client rendered.
/// </param>
/// <param name="Address">Required when typing the address.</param>
/// <param name="IsGstRegistered">
/// Explicit yes or no. Nullable so that "not answered" is distinguishable from "no", which
/// is what lets the server insist on a deliberate choice.
/// </param>
/// <param name="Gstin">Required when GST registered, rejected when not.</param>
/// <param name="GstRegistrationType">
/// Optional. The onboarding screen does not ask, so it defaults to Regular; billing needs
/// it later to tell a composition taxpayer from a regular one.
/// </param>
/// <param name="ConfirmDuplicateName">
/// Set when the caller has seen the "a company with this name already exists" warning and
/// confirmed it is a different business.
/// </param>
/// <param name="ConfirmDuplicateGstin">
/// Set when the caller has seen the duplicate-GSTIN warning and accepted the risk of two
/// sets of books against one registration.
/// </param>
/// <param name="RequestId">
/// Client-generated, repeated on retry. Makes a double-clicked or retried submission
/// create one company instead of two.
/// </param>
public sealed record CreateCompanyDto(
    string Name,
    AddressSource AddressSource,
    Guid? CopyAddressFromCompanyId,
    AddressDto? Address,
    bool? IsGstRegistered,
    string? Gstin,
    GstRegistrationType? GstRegistrationType,
    bool ConfirmDuplicateName,
    bool ConfirmDuplicateGstin,
    Guid RequestId);

/// <summary>
/// A warning the caller must acknowledge before the same request will be accepted.
///
/// Returned as a 409 rather than a hard rejection: the situation is legitimate but easy to
/// get wrong, so the server stops once and lets the person decide.
/// </summary>
public sealed record CompanyCreationWarningDto(string Field, string ConfirmWith, string Message);
