using RBiz.Domain.Entities;

namespace RBiz.Application.Interfaces;

/// <summary>
/// Persistence contract for <see cref="User"/>. Every lookup is tenant-scoped so one
/// tenant can never read another tenant's users.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a user for login by tenant code and email. Returns null when the tenant or
    /// the user does not exist, or either is inactive.
    /// </summary>
    Task<User?> GetForLoginAsync(string tenantCode, string email, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> GetByTenantAsync(Guid tenantId, int skip, int take, CancellationToken cancellationToken = default);

    Task<int> CountByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(Guid tenantId, string email, CancellationToken cancellationToken = default);

    void Add(User user);
}
