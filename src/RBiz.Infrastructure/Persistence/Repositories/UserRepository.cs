using Microsoft.EntityFrameworkCore;
using RBiz.Application.Interfaces;
using RBiz.Domain.Entities;

namespace RBiz.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IUserRepository"/>
internal sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context) => _context = context;

    /// <summary>A user by id alone, for paths that run before a company is chosen.</summary>
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    /// <summary>
    /// Tracked, because callers go on to modify the user and save. Membership of the
    /// company is part of the predicate, so a user outside it behaves like a missing one.
    /// </summary>
    public Task<User?> GetByIdAsync(
        Guid id,
        Guid companyId,
        CancellationToken cancellationToken = default) =>
        _context.Users.FirstOrDefaultAsync(
            u => u.Id == id && u.Companies.Any(c => c.CompanyId == companyId && c.IsActive),
            cancellationToken);

    /// <summary>
    /// Sign-in lookup by mobile number. Only an active account is returned, so a disabled
    /// user is refused in the query rather than after loading the row.
    /// </summary>
    public Task<User?> GetForLoginAsync(string mobile, CancellationToken cancellationToken = default) =>
        _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Mobile == mobile && u.IsActive, cancellationToken);

    public Task<bool> MobileExistsAsync(string mobile, CancellationToken cancellationToken = default) =>
        _context.Users.AnyAsync(u => u.Mobile == mobile, cancellationToken);

    public Task<bool> EmailExistsAsync(
        string email,
        Guid? excludeUserId = null,
        CancellationToken cancellationToken = default) =>
        _context.Users.AnyAsync(
            u => u.Email == email && (excludeUserId == null || u.Id != excludeUserId),
            cancellationToken);

    /// <summary>
    /// Users of one company, reached through the membership join. Read-only, so no change
    /// tracking snapshots are built for rows that will only be serialised.
    /// </summary>
    public async Task<IReadOnlyList<User>> GetByCompanyAsync(
        Guid companyId,
        int skip,
        int take,
        CancellationToken cancellationToken = default) =>
        await _context.UserCompanies
            .AsNoTracking()
            .Where(uc => uc.CompanyId == companyId && uc.IsActive)
            .OrderBy(uc => uc.User!.FullName)
            .Skip(skip)
            .Take(take)
            .Select(uc => uc.User!)
            .ToListAsync(cancellationToken);

    public Task<int> CountByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        _context.UserCompanies.CountAsync(
            uc => uc.CompanyId == companyId && uc.IsActive,
            cancellationToken);

    public void Add(User user) => _context.Users.Add(user);
}
