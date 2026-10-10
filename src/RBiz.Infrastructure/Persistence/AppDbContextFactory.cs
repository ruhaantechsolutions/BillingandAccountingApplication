using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using RBiz.Application.Interfaces;

namespace RBiz.Infrastructure.Persistence;

/// <summary>
/// Builds an <see cref="AppDbContext"/> for the "dotnet ef" tooling.
///
/// Without this, EF would have to boot the whole web host to find the context, which
/// means a migration command would fail on anything unrelated to the database - a missing
/// JWT signing key, for instance. Design-time tooling has no business depending on that.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string ConnectionStringName = "DefaultConnection";

    /// <summary>
    /// Used only when generating a migration, where EF needs the provider but never opens
    /// a connection. "dotnet ef database update" overrides this with the real value from
    /// configuration or the environment.
    /// </summary>
    private const string FallbackConnectionString =
        "Server=localhost;Database=RBiz;Trusted_Connection=True;TrustServerCertificate=True";

    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();

        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = FallbackConnectionString;
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;

        // The context needs these two collaborators, but at design time nothing is saved,
        // so a no-op user and the system clock are all that is required.
        return new AppDbContext(options, new DesignTimeCurrentUser(), TimeProvider.System);
    }

    /// <summary>
    /// Reads configuration the same way the API does, so a migration run against a real
    /// database uses the same connection string the application would.
    /// </summary>
    private static IConfiguration BuildConfiguration()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        // The WebAPI project holds the settings files; this walks from the Infrastructure
        // project folder that "dotnet ef" runs in over to its sibling.
        var webApiPath = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "RBiz.WebAPI"));

        var builder = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(webApiPath, "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine(webApiPath, $"appsettings.{environment}.json"), optional: true)
            // Lets CI or a deployment pass the connection string without editing a file:
            // ConnectionStrings__DefaultConnection=...
            .AddEnvironmentVariables();

        return builder.Build();
    }

    /// <summary>
    /// Stands in for the signed-in user during design-time operations, where there isn't one.
    /// </summary>
    private sealed class DesignTimeCurrentUser : ICurrentUser
    {
        public Guid? UserId => null;

        public Guid? TenantId => null;

        public string? Email => null;

        public bool IsAuthenticated => false;

        public Guid GetRequiredTenantId() =>
            throw new InvalidOperationException(
                "No tenant is available at design time. This context is only for EF tooling.");
    }
}
