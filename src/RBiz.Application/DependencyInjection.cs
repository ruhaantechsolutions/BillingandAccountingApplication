using Microsoft.Extensions.DependencyInjection;
using RBiz.Application.Interfaces;
using RBiz.Application.Services;

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
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        return services;
    }
}
