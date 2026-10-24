using RBiz.Domain.Entities;

namespace RBiz.Application.Interfaces;

/// <summary>
/// Issues signed access tokens. Declared here so the Application layer can ask for a
/// token without referencing any JWT library - the implementation lives in Infrastructure.
/// </summary>
public interface IJwtTokenGenerator
{
    /// <summary>
    /// Builds a signed token for the given user, optionally scoped to one of their
    /// companies.
    /// </summary>
    /// <param name="user">The signed-in person.</param>
    /// <param name="companyId">
    /// The company the token may act on, or null straight after sign-in when none has been
    /// chosen. Services that read company data refuse a token without it.
    /// </param>
    /// <returns>The encoded token and the UTC instant it expires.</returns>
    (string Token, DateTime ExpiresAtUtc) GenerateToken(User user, Guid? companyId);
}
