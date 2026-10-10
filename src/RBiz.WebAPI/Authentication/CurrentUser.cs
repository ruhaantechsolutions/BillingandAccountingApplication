using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using RBiz.Application.Common;
using RBiz.Application.Interfaces;

namespace RBiz.WebAPI.Authentication;

/// <summary>
/// Reads the caller's identity out of the validated JWT. This lives in the WebAPI layer
/// because it is the only layer that knows about HttpContext; the Application layer sees
/// nothing but the <see cref="ICurrentUser"/> interface.
/// </summary>
internal sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId => GetGuidClaim(JwtRegisteredClaimNames.Sub, ClaimTypes.NameIdentifier);

    public Guid? TenantId => GetGuidClaim(AppClaimTypes.TenantId);

    public string? Email =>
        Principal?.FindFirst(JwtRegisteredClaimNames.Email)?.Value
        ?? Principal?.FindFirst(ClaimTypes.Email)?.Value;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid GetRequiredTenantId() =>
        TenantId ?? throw new UnauthorizedAccessException(
            "The request is not associated with a tenant. A valid access token is required.");

    /// <summary>
    /// Looks for the claim under any of the given names. ASP.NET Core may remap short JWT
    /// claim names onto the longer ClaimTypes URIs, so both spellings are checked.
    /// </summary>
    private Guid? GetGuidClaim(params string[] claimTypes)
    {
        var principal = Principal;

        if (principal is null)
        {
            return null;
        }

        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;

            if (Guid.TryParse(value, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }
}
