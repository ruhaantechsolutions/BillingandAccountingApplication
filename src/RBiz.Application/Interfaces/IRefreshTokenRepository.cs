using RBiz.Domain.Entities;

namespace RBiz.Application.Interfaces;

/// <summary>
/// Persistence contract for refresh tokens. Lookups are by hash, because the plain token
/// is never stored.
/// </summary>
public interface IRefreshTokenRepository
{
    /// <summary>
    /// Finds a token by its hash, including the owning user so the caller can check the
    /// account is still active without a second query.
    /// </summary>
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    void Add(RefreshToken token);

    /// <summary>
    /// Revokes every still-active token in a family in one statement.
    ///
    /// Used when a rotated token is replayed: at that point one copy has leaked and the
    /// safe assumption is that the whole chain is compromised.
    /// </summary>
    /// <returns>How many tokens were revoked.</returns>
    Task<int> RevokeFamilyAsync(
        Guid familyId,
        RefreshTokenRevocationReason reason,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken = default);
}
