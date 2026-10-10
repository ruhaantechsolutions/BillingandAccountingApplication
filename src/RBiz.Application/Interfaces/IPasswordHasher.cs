namespace RBiz.Application.Interfaces;

/// <summary>
/// Hashes and verifies passwords. An abstraction so the hashing algorithm can be
/// upgraded later without touching business code.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// Checks a plain-text password against a stored hash in constant time.
    /// </summary>
    bool Verify(string password, string passwordHash);
}
