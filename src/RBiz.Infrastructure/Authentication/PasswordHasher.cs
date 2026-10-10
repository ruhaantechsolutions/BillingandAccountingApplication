using Microsoft.AspNetCore.Identity;
using RBiz.Application.Interfaces;
using RBiz.Domain.Entities;
using IdentityPasswordHasher = Microsoft.AspNetCore.Identity.PasswordHasher<RBiz.Domain.Entities.User>;

namespace RBiz.Infrastructure.Authentication;

/// <summary>
/// Password hashing backed by ASP.NET Core Identity's PBKDF2 implementation, which
/// applies a per-password salt, a high iteration count and a constant-time comparison.
/// Wrapped behind <see cref="IPasswordHasher"/> so none of that leaks into business code.
/// </summary>
internal sealed class PasswordHasher : IPasswordHasher
{
    private readonly IdentityPasswordHasher _hasher = new();

    // The hasher needs a user instance but never reads it for the PBKDF2 format, so a
    // single shared instance is enough and avoids an allocation per call.
    private static readonly User HashTarget = new();

    public string Hash(string password) => _hasher.HashPassword(HashTarget, password);

    public bool Verify(string password, string passwordHash)
    {
        var result = _hasher.VerifyHashedPassword(HashTarget, passwordHash, password);

        // SuccessRehashNeeded means the stored hash used older parameters but the password
        // was correct, so it still counts as a valid login.
        return result is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
