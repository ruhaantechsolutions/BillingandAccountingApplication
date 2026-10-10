using System.Security.Cryptography;
using System.Text;
using RBiz.Application.Interfaces;

namespace RBiz.Infrastructure.Authentication;

/// <inheritdoc cref="ISecureTokenFactory"/>
internal sealed class SecureTokenFactory : ISecureTokenFactory
{
    /// <summary>
    /// 32 bytes - 256 bits - of randomness. Far beyond guessing, so the stored hash does
    /// not need to be slow the way a password hash does.
    /// </summary>
    private const int TokenByteLength = 32;

    public (string Token, string TokenHash) Create()
    {
        // Cryptographically secure by construction; Random would be predictable enough to
        // forge another user's token.
        var bytes = RandomNumberGenerator.GetBytes(TokenByteLength);

        // URL-safe so the value survives being put in a header, a JSON body or a query
        // string without escaping.
        var token = Base64UrlEncode(bytes);

        return (token, Hash(token));
    }

    /// <summary>
    /// Plain SHA-256. Correct here precisely because the input is high-entropy random:
    /// there is no dictionary to attack, and a deliberately slow hash would be paid on
    /// every reconnect of every device for nothing.
    /// </summary>
    public string Hash(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));

        return Convert.ToHexString(hash);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
