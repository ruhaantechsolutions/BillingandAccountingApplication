using Asp.Versioning;

namespace RBiz.WebAPI.Extensions;

/// <summary>
/// API versioning configuration.
///
/// Versioning here is dynamic: a controller declares the version it serves with
/// [ApiVersion("2.0")] and the route template picks it up automatically. Nothing in this
/// file, and nothing in Program.cs, has to be edited to add a version.
/// </summary>
internal static class ApiVersioningExtensions
{
    /// <summary>
    /// The route prefix every controller uses. The {version:apiVersion} token is
    /// substituted from the controller's [ApiVersion] attribute.
    /// </summary>
    public const string RouteTemplate = "api/v{version:apiVersion}/[controller]";

    public static IServiceCollection AddApiVersioningConfiguration(this IServiceCollection services)
    {
        services
            .AddApiVersioning(options =>
            {
                // Used when a controller declares no version of its own. Every
                // controller here declares one explicitly, and the version is a required
                // segment of the route, so a request can never arrive without it.
                options.DefaultApiVersion = new ApiVersion(1, 0);

                // Advertises supported and deprecated versions in the response headers, so
                // a client can discover it is on the way out without reading the docs.
                options.ReportApiVersions = true;

                // The version is taken from the URL segment. Keeping to one source avoids
                // the ambiguity of a request that asks for v1 in the path and v2 in a header.
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                // Produces group names like "v1" and "v2" that the Swagger document
                // generator below keys off.
                options.GroupNameFormat = "'v'VVV";

                // Rewrites "api/v{version}/users" to "api/v1/users" in the generated
                // document, so the Try It Out button sends a real URL.
                options.SubstituteApiVersionInUrl = true;
            });

        return services;
    }
}
