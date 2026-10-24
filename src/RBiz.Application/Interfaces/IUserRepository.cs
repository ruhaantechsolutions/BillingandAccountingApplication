using RBiz.Domain.Entities;

namespace RBiz.Application.Interfaces;

/// <summary>
/// Persistence contract for <see cref="User"/>. Every lookup is company-scoped so one
/// company can never read another company's users.
/// </summary>
public interface IUserRepository
{
    /// <summary>A user by id alone, for paths that run before a company is selected.</summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// A user by id, restricted to members of the given company. A caller from another
    /// company gets null, so a guessed id behaves exactly like a missing one.
    /// </summary>
    Task<User?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an active user by mobile number for sign-in. Returns null when no such user
    /// exists or the account is inactive.
    /// </summary>
    Task<User?> GetForLoginAsync(string mobile, CancellationToken cancellationToken = default);

    /// <summary>True when the mobile number is already registered.</summary>
    Task<bool> MobileExistsAsync(string mobile, CancellationToken cancellationToken = default);

    /// <summary>True when the email is already registered to another account.</summary>
    Task<bool> EmailExistsAsync(
        string email,
        Guid? excludeUserId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> GetByCompanyAsync(Guid companyId, int skip, int take, CancellationToken cancellationToken = default);

    Task<int> CountByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);

    void Add(User user);
}
