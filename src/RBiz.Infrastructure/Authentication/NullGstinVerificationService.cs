using Microsoft.Extensions.Logging;
using RBiz.Application.Interfaces;
using RBiz.Domain.Entities;

namespace RBiz.Infrastructure.Authentication;

/// <summary>
/// The shipped GSTIN verification: it reports that the live register was not consulted.
///
/// This is honest rather than lazy. Confirming a GSTIN against the register needs the
/// government API, which is reached through a licensed GST Suvidha Provider; there is no
/// openly licensed service to call. Everything that can be checked without one - format,
/// state code, embedded PAN and check digit - is already done by GstinValidator before a
/// registration reaches here.
///
/// Replace this registration with a real provider and nothing else in the application has
/// to change: company creation already stores whatever status comes back and never treats
/// "could not check" as "invalid".
/// </summary>
internal sealed class NullGstinVerificationService : IGstinVerificationService
{
    private readonly ILogger<NullGstinVerificationService> _logger;

    public NullGstinVerificationService(ILogger<NullGstinVerificationService> logger) =>
        _logger = logger;

    public Task<GstinVerificationStatus> VerifyAsync(
        string gstin,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "No GST verification provider is configured, so {Gstin} is recorded as format-checked only.",
            gstin);

        return Task.FromResult(GstinVerificationStatus.VerificationUnavailable);
    }
}
