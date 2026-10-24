using Microsoft.Extensions.DependencyInjection;
using RBiz.Application.Interfaces;
using RBiz.Application.Services;
using RBiz.Application.Sync;
using RBiz.Application.Sync.Interfaces;

namespace RBiz.Application;

/// <summary>
/// Registers everything this layer owns. Program.cs chains this with the Infrastructure
/// equivalent, so neither layer leaks its internals into the host's startup code.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Scoped: one instance per request, matching the lifetime of the DbContext the
        // repositories behind these services depend on.
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        services.AddOfflineSync();

        return services;
    }

    /// <summary>
    /// Offline-synchronisation services, kept in their own method so the PWA/offline
    /// feature can be found, changed or removed without touching the rest of the layer.
    /// </summary>
    private static IServiceCollection AddOfflineSync(this IServiceCollection services)
    {
        // SyncOptions is bound by the host, which is the only layer that knows where
        // configuration comes from.
        services.AddScoped<ISyncService, SyncService>();

        return services;
    }
}
