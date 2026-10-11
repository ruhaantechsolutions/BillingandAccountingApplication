using Microsoft.EntityFrameworkCore;
using RBiz.Application.Interfaces;
using RBiz.Domain.Entities;

namespace RBiz.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IUserRepository"/>
internal sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context) => _context = context;

    /// <summary>
    /// Tracked, because callers of this overload go on to modify the user and save.
    /// The tenant is part of the predicate, so a mismatched id behaves exactly like a
    /// missing one.
    /// </summary>
    public Task<User?> GetByIdAsync(
        Guid id,
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        _context.Users.FirstOrDefaultAsync(
            u => u.Id == id && u.TenantId == tenantId,
            cancellationToken);

    public Task<User?> GetForLoginAsync(
        string tenantCode,
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalisedCode = tenantCode.Trim().ToUpperInvariant();
        var normalisedEmail = email.Trim().ToUpperInvariant();

        // Both the tenant and the user must be active, enforced in the query so an
        // inactive account never leaves the database.
        return _context.Users
            .AsNoTracking()
            .Where(u => u.Email == normalisedEmail
                        && u.IsActive
                        && u.Tenant!.Code == normalisedCode
                        && u.Tenant.IsActive)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetByTenantAsync(
        Guid tenantId,
        int skip,
        int take,
        CancellationToken cancellationToken = default) =>
        await _context.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.FullName)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<int> CountByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        _context.Users.CountAsync(u => u.TenantId == tenantId, cancellationToken);

    public Task<bool> EmailExistsAsync(
        Guid tenantId,
        string email,
        Guid? excludeUserId = null,
        CancellationToken cancellationToken = default) =>
        _context.Users.AnyAsync(
            u => u.TenantId == tenantId
                 && u.Email == email
                 && (excludeUserId == null || u.Id != excludeUserId),
            cancellationToken);

    public void Add(User user) => _context.Users.Add(user);
}
