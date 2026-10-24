namespace RBiz.Application.Dtos;

/// <summary>
/// Credentials posted to the login endpoint.
/// </summary>
/// <param name="Mobile">Ten-digit mobile number. The login identity.</param>
/// <param name="Password">Plain-text password, over TLS only.</param>
/// <param name="DeviceId">
/// Optional identifier for the device signing in, the same value it uses for sync. The
/// refresh token is bound to it, so one device can later be signed out on its own.
/// </param>
public sealed record LoginRequestDto(string Mobile, string Password, string? DeviceId = null);

/// <summary>
/// Creates an account. No company is created here - a new account lands on the company
/// registration screen with nothing yet, which is exactly what the sign-up flow expects.
/// </summary>
/// <param name="FullName">The person's name, not a business name.</param>
/// <param name="Mobile">Ten-digit mobile number. Must not already be registered.</param>
/// <param name="Password">Chosen password.</param>
/// <param name="Email">Optional. Used for notifications and recovery, not for signing in.</param>
/// <param name="DeviceId">Optional device identifier, as for login.</param>
public sealed record RegisterRequestDto(
    string FullName,
    string Mobile,
    string Password,
    string? Email = null,
    string? DeviceId = null);

/// <summary>
/// Exchanges a refresh token for a fresh access token.
/// </summary>
/// <param name="RefreshToken">The refresh token issued by login or a previous refresh.</param>
/// <param name="DeviceId">Device presenting the token. Must match the one it was issued to.</param>
public sealed record RefreshRequestDto(string RefreshToken, string? DeviceId = null);

/// <summary>Revokes a refresh token and everything descended from the same login.</summary>
public sealed record LogoutRequestDto(string RefreshToken);

/// <summary>
/// Issued tokens and the context they were issued for.
/// </summary>
/// <param name="AccessToken">Short-lived bearer token for API calls.</param>
/// <param name="ExpiresAtUtc">When the access token stops being accepted.</param>
/// <param name="RefreshToken">
/// Long-lived token used to obtain the next access token. Rotated on every refresh, so a
/// client must always store the newest value it was given and discard the old one.
/// </param>
/// <param name="RefreshTokenExpiresAtUtc">When the refresh token stops working.</param>
/// <param name="UserId">Signed-in user.</param>
/// <param name="Mobile">The number signed in with.</param>
/// <param name="FullName">Display name.</param>
/// <param name="ActiveCompanyId">
/// The company this access token acts on, or null straight after signing in. Null is what
/// tells the client to send the user to company registration or to a company chooser.
/// </param>
/// <param name="Companies">
/// Every company the user can reach. Feeds the switcher and the "existing companies"
/// panel without a second call.
/// </param>
public sealed record AuthResponseDto(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    Guid UserId,
    string Mobile,
    string FullName,
    Guid? ActiveCompanyId,
    IReadOnlyList<CompanySummaryDto> Companies);
