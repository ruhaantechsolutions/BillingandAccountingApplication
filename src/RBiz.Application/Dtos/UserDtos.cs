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

/// <summary>
/// Payload for changing a user's email.
///
/// Separate from <see cref="UpdateUserDto"/> on purpose: the email is the login
/// identity, so changing it is a different kind of act from editing a display name and
/// should not be possible as a side effect of a general update.
/// </summary>
public sealed record UpdateUserEmailDto(string Email);
