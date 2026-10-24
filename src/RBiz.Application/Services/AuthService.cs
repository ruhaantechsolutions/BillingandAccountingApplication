using Microsoft.Extensions.Logging;
using RBiz.Application.Dtos;
using RBiz.Application.Interfaces;
using RBiz.Application.Mapping;
using RBiz.Domain.Entities;
using RBiz.Domain.Exceptions;
using RBiz.Domain.Utilities;

namespace RBiz.Application.Services;

/// <inheritdoc cref="IAuthService"/>
public sealed class AuthService : IAuthService
{
    // One deliberately vague message whichever half of the credentials was wrong. Saying
    // which would let a caller discover registered mobile numbers.
    private const string InvalidCredentials = "The mobile number or password is incorrect.";

    private const string InvalidRefreshToken = "Your session has expired. Please sign in again.";

    private const string UnknownDevice = "unknown";

    private const int MinPasswordLength = 8;

    private readonly IUserRepository _users;
    private readonly ICompanyRepository _companies;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly ISecureTokenFactory _secureTokenFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly IRefreshTokenLifetime _lifetime;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        ICompanyRepository companies,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        ISecureTokenFactory secureTokenFactory,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        IRefreshTokenLifetime lifetime,
        ICurrentUser currentUser,
        ILogger<AuthService> logger)
    {
        _users = users;
        _companies = companies;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _secureTokenFactory = secureTokenFactory;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _lifetime = lifetime;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<AuthResponseDto> RegisterAsync(
        RegisterRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var fullName = request.FullName?.Trim() ?? string.Empty;
        var mobile = IndianMobileNumber.Normalise(request.Mobile);
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToUpperInvariant();
        var password = request.Password ?? string.Empty;

        var errors = new Dictionary<string, string[]>();

        if (fullName.Length == 0)
        {
            errors[nameof(request.FullName)] = ["Enter your name."];
        }

        if (!IndianMobileNumber.IsValid(mobile))
        {
            errors[nameof(request.Mobile)] = ["Enter a valid 10-digit mobile number."];
        }

        if (password.Length < MinPasswordLength)
        {
            errors[nameof(request.Password)] =
                [$"Password must be at least {MinPasswordLength} characters long."];
        }

        if (email is not null && !email.Contains('@'))
        {
            errors[nameof(request.Email)] = ["Enter a valid email address, or leave it blank."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        if (await _users.MobileExistsAsync(mobile, cancellationToken))
        {
            throw new ConflictException(
                "This mobile number is already registered. Sign in instead, or reset your password.");
        }

        if (email is not null && await _users.EmailExistsAsync(email, cancellationToken: cancellationToken))
        {
            throw new ConflictException("This email address is already registered to another account.");
        }

        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Mobile = mobile,
            FullName = fullName,
            Email = email,
            PasswordHash = _passwordHasher.Hash(password),
            IsActive = true
        };

        _users.Add(user);

        // No company is created. A new account lands on company registration with an empty
        // list, which is exactly the flow the sign-up screen expects.
        var response = IssueTokens(user, NormaliseDeviceId(request.DeviceId), null, null, []);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Account {UserId} registered for mobile ending {Last4}.",
            user.Id,
            mobile[^4..]);

        return response;
    }

    public async Task<AuthResponseDto> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var mobile = IndianMobileNumber.Normalise(request.Mobile);

        if (!IndianMobileNumber.IsValid(mobile) || string.IsNullOrEmpty(request.Password))
        {
            throw new ValidationException(InvalidCredentials);
        }

        var user = await _users.GetForLoginAsync(mobile, cancellationToken);

        // The password is verified even when no user was found, against a throwaway hash,
        // so a wrong number and a wrong password take the same time. Without this, response
        // timing alone reveals which numbers are registered.
        var hashToCheck = user?.PasswordHash ?? _passwordHasher.Hash(InvalidCredentials);

        if (!_passwordHasher.Verify(request.Password, hashToCheck) || user is null)
        {
            _logger.LogWarning("Failed sign-in attempt for mobile ending {Last4}.",
                mobile.Length >= 4 ? mobile[^4..] : mobile);

            throw new ValidationException(InvalidCredentials);
        }

        // Returned with the tokens so the client can decide between the company chooser and
        // the registration screen without a second call.
        var companies = await LoadCompaniesAsync(user.Id, cancellationToken);

        var response = IssueTokens(user, NormaliseDeviceId(request.DeviceId), null, null, companies);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} signed in with {CompanyCount} companies available.",
            user.Id,
            companies.Count);

        return response;
    }

    public async Task<AuthResponseDto> SwitchCompanyAsync(
        Guid companyId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("A valid access token is required to switch company.");

        var user = await _users.GetByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("The signed-in account is no longer available.");

        // Membership is re-checked here, not taken from the id the client sent. This is the
        // single point that decides what the next token may reach.
        var company = await _companies.GetForUserAsync(companyId, userId, cancellationToken)
            ?? throw new NotFoundException(nameof(Company), companyId);

        if (!company.IsActive)
        {
            throw new ConflictException($"'{company.Name}' is not active and cannot be opened.");
        }

        var companies = await LoadCompaniesAsync(userId, cancellationToken);

        var response = IssueTokens(user, NormaliseDeviceId(deviceId), companyId, null, companies);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} switched to company {CompanyId}.", userId, companyId);

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

        var tokenHash = _secureTokenFactory.Hash(request.RefreshToken);
        var stored = await _refreshTokens.GetByHashAsync(tokenHash, cancellationToken);

        if (stored is null)
        {
            _logger.LogWarning("Refresh attempted with an unrecognised token.");
            throw new ValidationException(InvalidRefreshToken);
        }

        // An already-rotated token presented again means a copy leaked, and we cannot tell
        // this caller from the victim, so everything descended from that login is revoked.
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
            _logger.LogInformation("Expired refresh token presented for user {UserId}.", stored.UserId);
            throw new ValidationException(InvalidRefreshToken);
        }

        var deviceId = NormaliseDeviceId(request.DeviceId);

        if (!string.Equals(stored.DeviceId, UnknownDevice, StringComparison.Ordinal) &&
            !string.Equals(stored.DeviceId, deviceId, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Refresh token for user {UserId} presented from device {PresentedDevice} but issued to {IssuedDevice}.",
                stored.UserId,
                deviceId,
                stored.DeviceId);

            throw new ValidationException(InvalidRefreshToken);
        }

        var user = stored.User;

        if (user is null || !user.IsActive || user.IsDeleted)
        {
            _logger.LogWarning("Refresh refused for user {UserId}: the account is unavailable.", stored.UserId);
            throw new ValidationException(InvalidRefreshToken);
        }

        var companies = await LoadCompaniesAsync(user.Id, cancellationToken);

        // The company the session was on is carried across, so a device coming back online
        // resumes where it was instead of asking the user to choose again. Membership may
        // have been withdrawn while it was away, so it is re-checked rather than trusted.
        var activeCompanyId = stored.ActiveCompanyId is { } previous
                              && companies.Any(c => c.Id == previous)
            ? previous
            : (Guid?)null;

        var response = IssueTokens(user, stored.DeviceId, activeCompanyId, stored.FamilyId, companies, out var replacement);

        stored.RevokedAtUtc = nowUtc;
        stored.RevokedReason = RefreshTokenRevocationReason.Rotated;
        stored.ReplacedByTokenId = replacement.Id;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Refreshed access token for user {UserId} on device {DeviceId}.",
            user.Id,
            stored.DeviceId);

        return response;
    }

    public async Task LogoutAsync(LogoutRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return;
        }

        var tokenHash = _secureTokenFactory.Hash(request.RefreshToken);
        var stored = await _refreshTokens.GetByHashAsync(tokenHash, cancellationToken);

        // Signing out something already gone is a success. Reporting "no such token" would
        // confirm which tokens exist.
        if (stored is null)
        {
            return;
        }

        await _refreshTokens.RevokeFamilyAsync(
            stored.FamilyId,
            RefreshTokenRevocationReason.SignedOut,
            _timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} signed out on device {DeviceId}.", stored.UserId, stored.DeviceId);
    }

    private async Task<IReadOnlyList<CompanySummaryDto>> LoadCompaniesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _companies.GetForUserAsync(userId, cancellationToken);
    }

    private AuthResponseDto IssueTokens(
        User user,
        string deviceId,
        Guid? activeCompanyId,
        Guid? familyId,
        IReadOnlyList<CompanySummaryDto> companies) =>
        IssueTokens(user, deviceId, activeCompanyId, familyId, companies, out _);

    /// <summary>
    /// Mints an access token and a refresh token, adding the latter to the unit of work.
    /// The caller commits, so a sign-in or a rotation lands as one transaction.
    /// </summary>
    private AuthResponseDto IssueTokens(
        User user,
        string deviceId,
        Guid? activeCompanyId,
        Guid? familyId,
        IReadOnlyList<CompanySummaryDto> companies,
        out RefreshToken refreshToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        var (accessToken, accessExpiresAtUtc) = _tokenGenerator.GenerateToken(user, activeCompanyId);
        var (token, tokenHash) = _secureTokenFactory.Create();

        var tokenId = Guid.CreateVersion7();

        refreshToken = new RefreshToken
        {
            Id = tokenId,
            UserId = user.Id,
            TokenHash = tokenHash,
            DeviceId = deviceId,
            ActiveCompanyId = activeCompanyId,

            // A rotation stays in its original family; a fresh sign-in starts its own.
            FamilyId = familyId ?? tokenId,

            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc.Add(_lifetime.RefreshTokenLifetime)
        };

        _refreshTokens.Add(refreshToken);

        return new AuthResponseDto(
            accessToken,
            accessExpiresAtUtc,
            token,
            refreshToken.ExpiresAtUtc,
            user.Id,
            user.Mobile,
            user.FullName,
            activeCompanyId,
            companies);
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
