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
    /// <summary>
    /// The company the token is acting on. Absent immediately after sign-in, because a
    /// person may hold several companies and has not chosen one yet; present once they
    /// select a company, and replaced when they switch.
    /// </summary>
    public const string CompanyId = "company_id";
}
