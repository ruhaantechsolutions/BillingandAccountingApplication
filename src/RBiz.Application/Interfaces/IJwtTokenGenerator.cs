using RBiz.Domain.Entities;

namespace RBiz.Application.Interfaces;

/// <summary>
/// Issues signed access tokens. Declared here so the Application layer can ask for a
/// token without referencing any JWT library - the implementation lives in Infrastructure.
/// </summary>
public interface IJwtTokenGenerator
{
    /// <summary>
    /// Builds a signed token for the given user.
    /// </summary>
    /// <returns>The encoded token and the UTC instant it expires.</returns>
    (string Token, DateTime ExpiresAtUtc) GenerateToken(User user);
}
