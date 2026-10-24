using RBiz.Application.Dtos;

namespace RBiz.Application.Interfaces;

/// <summary>Company use cases for the signed-in user.</summary>
public interface ICompanyService
{
    /// <summary>
    /// Every company the caller can reach. Feeds the company switcher and the "existing
    /// companies" panel on the registration screen.
    /// </summary>
    Task<IReadOnlyList<CompanySummaryDto>> GetMyCompaniesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a company, its address, its GST registration where applicable, the caller's
    /// ownership of it and its primary location - all in one transaction, so a failure can
    /// never leave a company that is half set up.
    /// </summary>
    /// <exception cref="Domain.Exceptions.ValidationException">A field is missing or malformed.</exception>
    /// <exception cref="Domain.Exceptions.ConflictException">
    /// A duplicate name or GSTIN needs acknowledging first.
    /// </exception>
    Task<CompanyDto> CreateAsync(
        CreateCompanyDto request,
        CancellationToken cancellationToken = default);

    /// <summary>A single company the caller can reach.</summary>
    Task<CompanyDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
