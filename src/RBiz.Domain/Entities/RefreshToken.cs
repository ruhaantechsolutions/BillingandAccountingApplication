using RBiz.Domain.Common;

namespace RBiz.Domain.Entities;

/// <summary>Why a refresh token is no longer usable.</summary>
public enum RefreshTokenRevocationReason
{
    /// <summary>Exchanged for a new token during a normal refresh.</summary>
    Rotated = 1,

    /// <summary>The user signed out.</summary>
    SignedOut = 2,

    /// <summary>
    /// An already-rotated token in this family was presented again, which means a copy
    /// leaked. Every token in the family is revoked for this reason.
    /// </summary>
    ReuseDetected = 3
}

/// <summary>
/// A long-lived credential that lets a device obtain a fresh access token without the
/// user typing their password again.
///
/// This is what makes the offline PWA workable: an access token lasts an hour or two, so
/// a device that has been offline all day would otherwise come back with a dead token and
/// no way to upload the work queued on it.
///
/// Two deliberate choices:
///
/// It does NOT derive from SyncableEntity. A credential must never be replicated to a
/// device, and an offline cache is exactly where it would be forgotten.
///
/// It implements <see cref="INonAuditable"/>, so the token hash is never copied into the
/// audit trail.
/// </summary>
public class RefreshToken : INonAuditable
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// SHA-256 of the token, never the token itself. A leaked database must not hand over
    /// live sessions.
    ///
    /// A plain hash is the right choice here, unlike for a password: the token is 256 bits
    /// of cryptographic randomness, so there is nothing to brute-force, and a deliberately
    /// slow hash would be paid on every reconnect for no gain.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// Device the token was issued to, matching the deviceId used for sync. Lets a stolen
    /// token be traced to a device, and a single device be signed out.
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// The login this token descends from. Every rotation keeps the same family, so one
    /// replay can revoke the whole chain in a single statement.
    /// </summary>
    public Guid FamilyId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public RefreshTokenRevocationReason? RevokedReason { get; set; }

    /// <summary>The token issued in exchange for this one, for tracing a chain.</summary>
    public Guid? ReplacedByTokenId { get; set; }

    public User? User { get; set; }

    /// <summary>
    /// Usable only while unrevoked and unexpired. Evaluated in memory against a supplied
    /// clock rather than DateTime.UtcNow so expiry can be tested.
    /// </summary>
    public bool IsActive(DateTime nowUtc) => RevokedAtUtc is null && ExpiresAtUtc > nowUtc;
}
