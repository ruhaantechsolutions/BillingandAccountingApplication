namespace RBiz.Application.Common;

/// <summary>
/// Names of the custom claims the application puts in an access token.
///
/// Declared in the Application layer because the claim name is a contract between the
/// code that writes the token (Infrastructure) and the code that reads it (WebAPI).
/// Keeping it here stops the two from drifting apart over a string literal.
/// </summary>
public static class AppClaimTypes
{
    /// <summary>Carries the tenant the token was issued for.</summary>
    public const string TenantId = "tenant_id";
}
