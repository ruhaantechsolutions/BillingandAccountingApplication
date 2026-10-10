using Microsoft.Extensions.Logging;
using RBiz.Application.Dtos;
using RBiz.Application.Interfaces;
using RBiz.Domain.Exceptions;

namespace RBiz.Application.Services;

/// <inheritdoc cref="IAuthService"/>
public sealed class AuthService : IAuthService
{
    // One deliberately vague message for every failure reason. Telling a caller whether
    // it was the tenant, the email or the password that was wrong would let them probe
    // for valid accounts.
    private const string InvalidCredentials = "The tenant code, email address or password is incorrect.";

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        ILogger<AuthService> logger)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
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

        var (token, expiresAtUtc) = _tokenGenerator.GenerateToken(user);

        _logger.LogInformation(
            "User {UserId} of tenant {TenantId} signed in successfully.",
            user.Id,
            user.TenantId);

        return new AuthResponseDto(
            token,
            expiresAtUtc,
            user.Id,
            user.TenantId,
            user.Email,
            user.FullName);
    }
}
