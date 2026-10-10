using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RBiz.Application.Interfaces;
using RBiz.Infrastructure.Authentication;
using RBiz.Infrastructure.Persistence;
using RBiz.Infrastructure.Persistence.Repositories;

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

        // Injected rather than calling DateTime.UtcNow directly, so audit timestamps and
        // token lifetimes can be controlled in a test.
        services.TryAddSingletonTimeProvider();

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
