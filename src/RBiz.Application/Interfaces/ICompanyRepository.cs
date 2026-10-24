using RBiz.Application.Dtos;
using RBiz.Domain.Entities;

namespace RBiz.Application.Interfaces;

/// <summary>
/// Persistence for companies. Every lookup is scoped to the companies a given user can
/// reach, so a company id guessed by a caller finds nothing.
/// </summary>
public interface ICompanyRepository
{
    /// <summary>
    /// Companies the user is an active member of, ordered by name.
    ///
    /// Projected in the database to exactly the columns the switcher shows. This list is
    /// fetched on every sign-in and every company switch, so it materialises six columns
    /// rather than whole company graphs.
    /// </summary>
    Task<IReadOnlyList<CompanySummaryDto>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A single company the user can reach, with its address and GST registration loaded.
    /// Returns null when it does not exist or the user has no access - the two are
    /// deliberately indistinguishable to the caller.
    /// </summary>
    Task<Company?> GetForUserAsync(
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>True when the user already has a company with this name.</summary>
    Task<bool> NameExistsForUserAsync(
        Guid userId,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The name of a company of this user already holding that GSTIN, or null. Returns the
    /// name so the warning can say which company it clashes with.
    /// </summary>
    Task<string?> FindGstinOwnerNameForUserAsync(
        Guid userId,
        string gstin,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The company created by an earlier attempt at the same request, or null. This is how
    /// a retried submission returns the original company instead of making a second one.
    /// </summary>
    Task<Company?> FindByRequestIdAsync(
        Guid userId,
        Guid requestId,
        CancellationToken cancellationToken = default);

    void Add(Company company);
}
