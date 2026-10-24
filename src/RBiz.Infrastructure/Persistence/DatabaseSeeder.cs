using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RBiz.Application.Interfaces;
using RBiz.Domain.Entities;
using RBiz.Domain.Utilities;

namespace RBiz.Infrastructure.Persistence;

/// <summary>
/// Creates the first account so the API can be signed into straight after a migration.
///
/// Deliberately seeds no company. A new account is meant to land on company registration
/// with an empty list, and seeding one would hide the very flow this module exists to
/// provide. It is also not done through a migration's HasData: that would commit a fixed
/// password hash to source control and give every deployment the same credential.
/// </summary>
public sealed class DatabaseSeeder
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    /// <summary>
    /// Inserts the seed account when no user exists yet. Safe to call on every startup:
    /// it is a no-op once any account is present.
    /// </summary>
    public async Task SeedAsync(
        string fullName,
        string mobile,
        string password,
        string? email,
        CancellationToken cancellationToken = default)
    {
        if (await _context.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var normalisedMobile = IndianMobileNumber.Normalise(mobile);

        // The seed is refused rather than stored wrong: a number login would later reject
        // would leave an account nobody can sign into.
        if (!IndianMobileNumber.IsValid(normalisedMobile))
        {
            _logger.LogWarning(
                "Seed skipped: '{Mobile}' is not a valid 10-digit mobile number.",
                mobile);

            return;
        }

        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Mobile = normalisedMobile,
            FullName = fullName,
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToUpperInvariant(),
            PasswordHash = _passwordHasher.Hash(password),
            IsActive = true
        };

        _context.Users.Add(user);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Seeded account {UserId} for mobile ending {Last4}. No company is created; " +
            "sign in and register one.",
            user.Id,
            normalisedMobile[^4..]);
    }
}
