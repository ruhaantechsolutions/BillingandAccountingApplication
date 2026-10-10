namespace RBiz.Application.Dtos;

/// <summary>Credentials posted to the login endpoint.</summary>
/// <param name="TenantCode">Short code of the business, e.g. "RUHAAN".</param>
/// <param name="Email">Login email address.</param>
/// <param name="Password">Plain-text password, over TLS only.</param>
/// <param name="DeviceId">
/// Optional identifier for the device signing in, the same value it uses for sync. The
/// refresh token is bound to it, so a single device can later be signed out on its own.
/// </param>
public sealed record LoginRequestDto(
    string TenantCode,
    string Email,
    string Password,
    string? DeviceId = null);

/// <summary>
/// Exchanges a refresh token for a fresh access token.
/// </summary>
/// <param name="RefreshToken">The refresh token issued by login or a previous refresh.</param>
/// <param name="DeviceId">
/// Device presenting the token. Must match the device it was issued to.
/// </param>
public sealed record RefreshRequestDto(string RefreshToken, string? DeviceId = null);

/// <summary>Revokes a refresh token and everything descended from the same login.</summary>
public sealed record LogoutRequestDto(string RefreshToken);

/// <summary>
/// Issued tokens and the context they were issued for.
/// </summary>
/// <param name="AccessToken">Short-lived bearer token for API calls.</param>
/// <param name="ExpiresAtUtc">When the access token stops being accepted.</param>
/// <param name="RefreshToken">
/// Long-lived token used to obtain the next access token. Rotated on every refresh, so
/// a client must always store the newest value it was given and discard the old one.
/// </param>
/// <param name="RefreshTokenExpiresAtUtc">
/// When the refresh token stops working. Past this point the user must sign in again,
/// so it bounds how long a device may stay offline and still recover.
/// </param>
/// <param name="UserId">Signed-in user.</param>
/// <param name="TenantId">Tenant the session is scoped to.</param>
/// <param name="Email">Signed-in user's email.</param>
/// <param name="FullName">Signed-in user's display name.</param>
public sealed record AuthResponseDto(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    Guid UserId,
    Guid TenantId,
    string Email,
    string FullName);
