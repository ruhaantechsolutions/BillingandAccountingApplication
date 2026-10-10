namespace RBiz.Application.Dtos;

/// <summary>
/// User as returned by the API. Deliberately has no password field so a hash can
/// never leak through a response.
/// </summary>
public sealed record UserDto(
    Guid Id,
    Guid TenantId,
    string Email,
    string FullName,
    bool IsActive,
    DateTime CreatedAtUtc);

/// <summary>Payload for creating a user inside the caller's tenant.</summary>
public sealed record CreateUserDto(string Email, string FullName, string Password);

/// <summary>Payload for updating a user.</summary>
public sealed record UpdateUserDto(string FullName, bool IsActive);
