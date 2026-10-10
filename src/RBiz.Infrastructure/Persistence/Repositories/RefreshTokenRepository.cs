using Microsoft.EntityFrameworkCore;
using RBiz.Application.Interfaces;
using RBiz.Domain.Entities;

namespace RBiz.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IRefreshTokenRepository"/>
internal sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _context;

    public RefreshTokenRepository(AppDbContext context) => _context = context;

    /// <summary>
    /// Tracked, because the caller marks the token rotated and saves. The user is
    /// included so the account's active state can be rechecked without another query -
    /// an account disabled while a device was offline must not be revived by a refresh.
    ///
    /// IgnoreQueryFilters is needed on the Include: User is soft-deletable, and the
    /// filter would otherwise silently return a token with a null User for a deleted
    /// account, which reads as "no user" rather than "deleted user".
    /// </summary>
    public Task<RefreshToken?> GetByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default) =>
        _context.RefreshTokens
            .IgnoreQueryFilters()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public void Add(RefreshToken token) => _context.RefreshTokens.Add(token);

    /// <summary>
    /// Revokes the whole family in a single UPDATE rather than loading the rows. A chain
    /// can be long for a device that reconnects often, and none of the rows are needed
    /// in memory.
    /// </summary>
    public Task<int> RevokeFamilyAsync(
        Guid familyId,
        RefreshTokenRevocationReason reason,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken = default) =>
        _context.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(t => t.RevokedAtUtc, revokedAtUtc)
                    .SetProperty(t => t.RevokedReason, reason),
                cancellationToken);
}
