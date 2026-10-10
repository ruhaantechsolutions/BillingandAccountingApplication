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
