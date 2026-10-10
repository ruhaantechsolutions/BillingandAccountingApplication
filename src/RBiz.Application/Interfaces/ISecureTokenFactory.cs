namespace RBiz.Application.Interfaces;

/// <summary>
/// Creates and hashes the opaque tokens used as refresh credentials.
/// </summary>
public interface ISecureTokenFactory
{
    /// <summary>
    /// Produces a new token: the secret to hand to the client, and the hash to store.
    /// The plain value is returned once and never persisted.
    /// </summary>
    (string Token, string TokenHash) Create();

    /// <summary>
    /// Hashes a token presented by a client so it can be looked up against stored hashes.
    /// </summary>
    string Hash(string token);
}
