namespace RBiz.WebAPI.Extensions;

/// <summary>
/// Cross-origin access for the Angular PWA, which is served from its own origin
/// (localhost:4200 in development, a static host in production) and therefore cannot
/// call this API without it.
/// </summary>
internal static class CorsExtensions
{
    public const string PolicyName = "RBizPwa";

    public static IServiceCollection AddPwaCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var origins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        services.AddCors(options => options.AddPolicy(PolicyName, policy =>
        {
            if (origins.Length == 0)
            {
                // No origins configured means no cross-origin access. Failing closed is
                // the right default: a wildcard here would expose the whole API to any
                // site the user happens to have open.
                return;
            }

            policy
                .WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                // Angular's HttpClient cannot read a response header unless the server
                // lists it here. The API version headers let a client notice it is
                // talking to a deprecated version.
                .WithExposedHeaders("api-supported-versions", "api-deprecated-versions")
                // Caches the preflight so a sync loop does not send an OPTIONS before
                // every request.
                .SetPreflightMaxAge(TimeSpan.FromMinutes(10));

            // Note: credentials are not allowed, and must not be. Authentication is a
            // bearer token in a header, so cookies are unnecessary, and combining
            // AllowCredentials with a permissive origin list is how CORS gets dangerous.
        }));

        return services;
    }
}
