namespace RBiz.Application.Dtos;

/// <summary>Credentials posted to the login endpoint.</summary>
public sealed record LoginRequestDto(string TenantCode, string Email, string Password);

/// <summary>Issued access token and the context it was issued for.</summary>
public sealed record AuthResponseDto(
    string AccessToken,
    DateTime ExpiresAtUtc,
    Guid UserId,
    Guid TenantId,
    string Email,
    string FullName);
