using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RBiz.Application.Interfaces;
using RBiz.Domain.Entities;

namespace RBiz.Infrastructure.Persistence;

/// <summary>
/// Creates the first tenant and administrator so the API is usable straight after a
/// migration. Deliberately not seeded through a migration's HasData: that would commit a
/// fixed password hash to source control and give every deployment the same credential.
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
    /// Inserts the seed tenant and user when the database has no tenants yet. Safe to call
    /// on every startup: it is a no-op once any tenant exists.
    /// </summary>
    public async Task SeedAsync(
        string tenantName,
        string tenantCode,
        string adminEmail,
        string adminPassword,
        CancellationToken cancellationToken = default)
    {
        if (await _context.Tenants.AnyAsync(cancellationToken))
        {
            return;
        }

        var tenant = new Tenant
        {
            Id = Guid.CreateVersion7(),
            Name = tenantName,
            Code = tenantCode.ToUpperInvariant(),
            IsActive = true
        };

        var admin = new User
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Id,
            Email = adminEmail.ToUpperInvariant(),
            FullName = "Administrator",
            PasswordHash = _passwordHasher.Hash(adminPassword),
            IsActive = true
        };

        _context.Tenants.Add(tenant);
        _context.Users.Add(admin);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Seeded tenant {TenantCode} with administrator {Email}.",
            tenant.Code,
            admin.Email);
    }
}
