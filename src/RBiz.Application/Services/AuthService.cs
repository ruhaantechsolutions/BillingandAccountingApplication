using Microsoft.Extensions.Logging;
using RBiz.Application.Dtos;
using RBiz.Application.Interfaces;
using RBiz.Domain.Entities;
using RBiz.Domain.Exceptions;

namespace RBiz.Application.Services;

/// <inheritdoc cref="IAuthService"/>
public sealed class AuthService : IAuthService
{
    // One deliberately vague message for every failure reason. Telling a caller whether
    // it was the tenant, the email or the password that was wrong would let them probe
    // for valid accounts.
    private const string InvalidCredentials = "The tenant code, email address or password is incorrect.";

    // Likewise for refresh: unknown, expired, revoked and wrong-device all look alike
    // from outside. The distinction is recorded in the log, not handed to the caller.
    private const string InvalidRefreshToken =
        "Your session has expired. Please sign in again.";

    private const string UnknownDevice = "unknown";

    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly ISecureTokenFactory _secureTokenFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly IRefreshTokenLifetime _lifetime;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        ISecureTokenFactory secureTokenFactory,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        IRefreshTokenLifetime lifetime,
        ILogger<AuthService> logger)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _secureTokenFactory = secureTokenFactory;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _lifetime = lifetime;
        _logger = logger;
    }

    public async Task<AuthResponseDto> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.TenantCode) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ValidationException(InvalidCredentials);
        }

        var user = await _users.GetForLoginAsync(request.TenantCode, request.Email, cancellationToken);

        // Note the password is verified even when no user was found, against a dummy hash,
        // so that a wrong email and a wrong password take the same amount of time. Without
        // this, response timing alone reveals which email addresses exist.
        var hashToCheck = user?.PasswordHash ?? _passwordHasher.Hash(InvalidCredentials);

        if (!_passwordHasher.Verify(request.Password, hashToCheck) || user is null)
        {
            // Logged at Warning with the attempted identifiers but never the password,
            // so repeated failures are visible to whoever watches the logs.
            _logger.LogWarning(
                "Failed login attempt for tenant {TenantCode} and email {Email}.",
                request.TenantCode,
                request.Email);

            throw new ValidationException(InvalidCredentials);
        }

        var deviceId = NormaliseDeviceId(request.DeviceId);

        // A fresh login starts a new token family, so signing in again on a second device
        // does not entangle the two sessions.
        var response = IssueTokens(user, deviceId, familyId: null);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User {UserId} of tenant {TenantId} signed in successfully from device {DeviceId}.",
            user.Id,
            user.TenantId,
            deviceId);

        return response;
    }

    public async Task<AuthResponseDto> RefreshAsync(
        RefreshRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new ValidationException(InvalidRefreshToken);
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        // Looked up by hash: the plain token is never stored, so a dump of this table
        // cannot be replayed against the API.
        var tokenHash = _secureTokenFactory.Hash(request.RefreshToken);
        var stored = await _refreshTokens.GetByHashAsync(tokenHash, cancellationToken);

        if (stored is null)
        {
            _logger.LogWarning("Refresh attempted with an unrecognised token.");
            throw new ValidationException(InvalidRefreshToken);
        }

        // A token that was already rotated is being presented again. The legitimate
        // client would have discarded it, so a copy has leaked - and we cannot tell
        // whether this request is the attacker or the victim. Everything descended from
        // the original login is therefore revoked, forcing a fresh sign-in.
        if (stored.RevokedAtUtc is not null)
        {
            var revoked = await _refreshTokens.RevokeFamilyAsync(
                stored.FamilyId,
                RefreshTokenRevocationReason.ReuseDetected,
                nowUtc,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Replay of a revoked refresh token for user {UserId} on device {DeviceId}. " +
                "Revoked {RevokedCount} token(s) in family {FamilyId}.",
                stored.UserId,
                stored.DeviceId,
                revoked,
                stored.FamilyId);

            throw new ValidationException(InvalidRefreshToken);
        }

        if (stored.ExpiresAtUtc <= nowUtc)
        {
            _logger.LogInformation(
                "Expired refresh token presented for user {UserId}.",
                stored.UserId);

            throw new ValidationException(InvalidRefreshToken);
        }

        // Binding to the device means a token lifted from one device cannot be used from
        // another. Only enforced when the original login supplied a device.
        var deviceId = NormaliseDeviceId(request.DeviceId);

        if (!string.Equals(stored.DeviceId, UnknownDevice, StringComparison.Ordinal) &&
            !string.Equals(stored.DeviceId, deviceId, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Refresh token for user {UserId} presented from device {PresentedDevice} " +
                "but was issued to {IssuedDevice}.",
                stored.UserId,
                deviceId,
                stored.DeviceId);

            throw new ValidationException(InvalidRefreshToken);
        }

        var user = stored.User;

        // An account disabled while the device was offline must not be revived by a
        // refresh. The same applies to its tenant.
        if (user is null || !user.IsActive || user.IsDeleted)
        {
            _logger.LogWarning(
                "Refresh refused for user {UserId} because the account is unavailable.",
                stored.UserId);

            throw new ValidationException(InvalidRefreshToken);
        }

        // Rotation: the presented token is spent and replaced. A captured token is then
        // useful only until the real client next reconnects, at which point the replay
        // check above fires.
        var response = IssueTokens(user, stored.DeviceId, stored.FamilyId, out var replacement);

        stored.RevokedAtUtc = nowUtc;
        stored.RevokedReason = RefreshTokenRevocationReason.Rotated;
        stored.ReplacedByTokenId = replacement.Id;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Refreshed access token for user {UserId} on device {DeviceId}.",
            user.Id,
            stored.DeviceId);

        return response;
    }

    public async Task LogoutAsync(
        LogoutRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return;
        }

        var tokenHash = _secureTokenFactory.Hash(request.RefreshToken);
        var stored = await _refreshTokens.GetByHashAsync(tokenHash, cancellationToken);

        // Signing out something already gone is a success, not an error: a client that
        // retries a logout should not be told anything went wrong, and reporting
        // "no such token" would confirm which tokens exist.
        if (stored is null)
        {
            return;
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        await _refreshTokens.RevokeFamilyAsync(
            stored.FamilyId,
            RefreshTokenRevocationReason.SignedOut,
            nowUtc,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User {UserId} signed out on device {DeviceId}.",
            stored.UserId,
            stored.DeviceId);
    }

    private AuthResponseDto IssueTokens(User user, string deviceId, Guid? familyId) =>
        IssueTokens(user, deviceId, familyId, out _);

    /// <summary>
    /// Mints an access token and a refresh token, adding the latter to the unit of work.
    /// The caller commits, so a login or a rotation lands as one transaction.
    /// </summary>
    private AuthResponseDto IssueTokens(
        User user,
        string deviceId,
        Guid? familyId,
        out RefreshToken refreshToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        var (accessToken, accessExpiresAtUtc) = _tokenGenerator.GenerateToken(user);
        var (token, tokenHash) = _secureTokenFactory.Create();

        var tokenId = Guid.CreateVersion7();

        refreshToken = new RefreshToken
        {
            Id = tokenId,
            TenantId = user.TenantId,
            UserId = user.Id,
            TokenHash = tokenHash,
            DeviceId = deviceId,

            // A rotation stays in its original family; a fresh login starts its own.
            FamilyId = familyId ?? tokenId,

            CreatedAtUtc = nowUtc,

            // Each rotation extends the window, which keeps an actively-used device
            // signed in indefinitely. Carrying an absolute expiry forward from the
            // family's start would be stricter but would sign out a working device on a
            // fixed schedule. See the README for the trade-off.
            ExpiresAtUtc = nowUtc.Add(_lifetime.RefreshTokenLifetime)
        };

        _refreshTokens.Add(refreshToken);

        return new AuthResponseDto(
            accessToken,
            accessExpiresAtUtc,
            token,
            refreshToken.ExpiresAtUtc,
            user.Id,
            user.TenantId,
            user.Email,
            user.FullName);
    }

    /// <summary>
    /// Device ids are opaque and client-supplied, so they are trimmed, bounded and never
    /// trusted as anything but a label.
    /// </summary>
    private static string NormaliseDeviceId(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return UnknownDevice;
        }

        var trimmed = deviceId.Trim();

        return trimmed.Length <= 100 ? trimmed : trimmed[..100];
    }
}
