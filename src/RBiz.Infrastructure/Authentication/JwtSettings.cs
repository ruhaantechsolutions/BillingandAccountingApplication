using System.ComponentModel.DataAnnotations;

namespace RBiz.Infrastructure.Authentication;

/// <summary>
/// Token signing and validation settings, bound from the "Jwt" configuration section.
/// The data annotations are validated at startup so a missing or too-short signing key
/// fails the application immediately instead of at the first login attempt.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// A short signing key is the single most common way a JWT setup is broken; HS256
    /// needs at least 256 bits, so anything under 32 characters is rejected outright.
    /// </summary>
    public const int MinKeyLength = 32;

    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Audience { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [MinLength(MinKeyLength, ErrorMessage =
        "Jwt:Key must be at least 32 characters so the HMAC-SHA256 signature is not weakened.")]
    public string Key { get; init; } = string.Empty;

    [Range(1, 1440)]
    public int ExpiryMinutes { get; init; } = 60;

    /// <summary>
    /// How many days a refresh token remains valid.
    ///
    /// This is the ceiling on how long a device may stay offline and still upload its
    /// queued work: once it lapses the user must sign in again, which an offline device
    /// cannot do. Thirty days suits a field device that may go a few weeks between
    /// connections; shorten it if sessions should die sooner.
    /// </summary>
    [Range(1, 365)]
    public int RefreshTokenDays { get; init; } = 30;
}
