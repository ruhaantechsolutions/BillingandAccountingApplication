using Microsoft.EntityFrameworkCore;
using RBiz.Application.Dtos;
using RBiz.Application.Interfaces;
using RBiz.Domain.Entities;

namespace RBiz.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="ICompanyRepository"/>
internal sealed class CompanyRepository : ICompanyRepository
{
    private readonly AppDbContext _context;

    public CompanyRepository(AppDbContext context) => _context = context;

    /// <summary>
    /// One joined query projected to the columns the switcher needs. The address is joined
    /// here rather than loaded as a navigation, because a projection that forgets it
    /// silently returns a company with no city - which is what the list is mostly for.
    /// </summary>
    public async Task<IReadOnlyList<CompanySummaryDto>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await _context.UserCompanies
            .AsNoTracking()
            .Where(uc => uc.UserId == userId && uc.IsActive && uc.Company!.IsActive)
            .OrderBy(uc => uc.Company!.Name)
            .Select(uc => new CompanySummaryDto(
                uc.Company!.Id,
                uc.Company.Name,
                uc.Company.Address!.City,
                uc.Company.Address.State,
                uc.Company.GstStatus,
                uc.Role))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Tracked, because callers go on to modify the company and save. Membership is part
    /// of the predicate, so a company the caller cannot reach is indistinguishable from
    /// one that does not exist.
    /// </summary>
    public Task<Company?> GetForUserAsync(
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        _context.Companies
            .Include(c => c.Address)
            .Include(c => c.GstRegistration)
            .Include(c => c.Members.Where(m => m.UserId == userId))
            .FirstOrDefaultAsync(
                c => c.Id == companyId
                     && c.Members.Any(m => m.UserId == userId && m.IsActive),
                cancellationToken);

    public Task<bool> NameExistsForUserAsync(
        Guid userId,
        string name,
        CancellationToken cancellationToken = default) =>
        _context.UserCompanies
            .AsNoTracking()
            .AnyAsync(
                uc => uc.UserId == userId
                      && uc.IsActive
                      && uc.Company!.Name == name,
                cancellationToken);

    public Task<string?> FindGstinOwnerNameForUserAsync(
        Guid userId,
        string gstin,
        CancellationToken cancellationToken = default) =>
        _context.UserCompanies
            .AsNoTracking()
            .Where(uc => uc.UserId == userId
                         && uc.IsActive
                         && uc.Company!.GstRegistration!.Gstin == gstin)
            .Select(uc => uc.Company!.Name)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Finds what an earlier attempt at the same request created. IgnoreQueryFilters is
    /// not needed: a company deleted since is genuinely gone and a retry should fail
    /// rather than resurrect it.
    /// </summary>
    public Task<Company?> FindByRequestIdAsync(
        Guid userId,
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        _context.Companies
            .AsNoTracking()
            .Include(c => c.Address)
            .Include(c => c.GstRegistration)
            .FirstOrDefaultAsync(
                c => c.CreatedBy == userId && c.CreationRequestId == requestId,
                cancellationToken);

    public void Add(Company company) => _context.Companies.Add(company);
}
