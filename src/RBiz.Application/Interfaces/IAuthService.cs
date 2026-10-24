using RBiz.Application.Dtos;

namespace RBiz.Application.Interfaces;

/// <summary>Authentication use cases.</summary>
public interface IAuthService
{
    /// <summary>
    /// Verifies credentials and issues an access token.
    /// </summary>
    /// <exception cref="Domain.Exceptions.ValidationException">
    /// Thrown when the credentials are wrong or the account is unavailable.
    /// </exception>
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an account and signs it straight in, so the caller lands on company
    /// registration without a second round trip.
    /// </summary>
    /// <exception cref="Domain.Exceptions.ConflictException">
    /// The mobile number, or the email if one was given, is already registered.
    /// </exception>
    Task<AuthResponseDto> RegisterAsync(
        RegisterRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a new access token scoped to one of the caller's companies.
    ///
    /// This is what makes switching company safe: the company lives in the token, so the
    /// data a request can reach changes with it rather than only the name on screen.
    /// </summary>
    /// <exception cref="Domain.Exceptions.NotFoundException">
    /// The company does not exist, or the caller is not a member of it.
    /// </exception>
    Task<AuthResponseDto> SwitchCompanyAsync(
        Guid companyId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges a refresh token for a new access token, rotating the refresh token in
    /// the process.
    ///
    /// This is what lets a PWA recover after being offline longer than an access token
    /// lives, so it can still upload the changes queued on the device.
    /// </summary>
    /// <exception cref="Domain.Exceptions.ValidationException">
    /// Thrown when the token is unknown, expired, revoked, or belongs to another device.
    /// </exception>
    Task<AuthResponseDto> RefreshAsync(
        RefreshRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs a device out by revoking its refresh token and every token descended from
    /// the same login. Succeeds quietly for a token that is already gone.
    /// </summary>
    Task LogoutAsync(LogoutRequestDto request, CancellationToken cancellationToken = default);
}
