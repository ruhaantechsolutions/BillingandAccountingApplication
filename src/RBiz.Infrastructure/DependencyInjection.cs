using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RBiz.Application.Interfaces;
using RBiz.Application.Sync.Interfaces;
using RBiz.Infrastructure.Authentication;
using RBiz.Infrastructure.Persistence;
using RBiz.Infrastructure.Persistence.Repositories;
using RBiz.Infrastructure.Persistence.Sync;

namespace RBiz.Infrastructure;

/// <summary>
/// Registers every implementation this layer provides for the interfaces the Application
/// layer declares. Program.cs chains this with AddApplication, which is the only place
/// the two layers meet.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.AddAuthenticationServices(configuration);
        services.AddOfflineSync();

        // Injected rather than calling DateTime.UtcNow directly, so audit timestamps and
        // token lifetimes can be controlled in a test.
        services.TryAddSingletonTimeProvider();

        return services;
    }

    /// <summary>
    /// Offline-synchronisation infrastructure, deliberately in its own method so the
    /// PWA/offline feature is easy to find, change or remove as a unit.
    ///
    /// TO ADD AN ENTITY TO SYNC: write a provider deriving from SyncEntityProviderBase
    /// and register it below. Nothing else in the sync layer needs to change.
    /// </summary>
    private static IServiceCollection AddOfflineSync(this IServiceCollection services)
    {
        services.AddScoped<ISyncStore, SyncStore>();

        // Registered against the shared interface so the registry picks them up
        // automatically. Order here is the order a client receives entities in, so
        // parents come before the rows that reference them.
        services.AddScoped<ISyncEntityProvider, TenantSyncProvider>();
        services.AddScoped<ISyncEntityProvider, UserSyncProvider>();

        services.AddScoped<ISyncEntityRegistry, SyncEntityRegistry>();

        return services;
    }

    private static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                // Retries transient faults (failover, throttling, brief network loss)
                // instead of surfacing them to the caller as a 500.
                sql.EnableRetryOnFailure(maxRetryCount: 3, TimeSpan.FromSeconds(5), null);
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            }));

        // The DbContext is the unit of work; exposing it through the interface keeps the
        // Application layer unaware of EF Core.
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<AppDbContext>());

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();

        services.AddScoped<DatabaseSeeder>();

        return services;
    }

    private static IServiceCollection AddAuthenticationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateDataAnnotations()
            // Validates at startup rather than on first use, so a bad signing key is a
            // failed deployment instead of a runtime surprise.
            .ValidateOnStart();

        // Stateless and thread-safe, so a single instance serves every request.
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        return services;
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (services.All(descriptor => descriptor.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
