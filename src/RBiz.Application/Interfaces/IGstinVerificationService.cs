using RBiz.Domain.Entities;

namespace RBiz.Application.Interfaces;

/// <summary>
/// Checks a GSTIN against the live GST register.
///
/// Separate from the offline <c>GstinValidator</c> on purpose. Validation answers "is this
/// number well-formed", which needs nothing but arithmetic; verification answers "is this
/// number a live registration", which needs the register. There is no openly licensed
/// service for the second - the government API is reached through a licensed GSP - so the
/// shipped implementation reports <see cref="GstinVerificationStatus.VerificationUnavailable"/>
/// and a provider can be configured later without touching any business code.
///
/// Implementations must never throw for an unreachable service: onboarding continues with
/// the registration marked unverified rather than failing.
/// </summary>
public interface IGstinVerificationService
{
    Task<GstinVerificationStatus> VerifyAsync(
        string gstin,
        CancellationToken cancellationToken = default);
}
