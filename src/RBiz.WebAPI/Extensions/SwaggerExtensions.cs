using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RBiz.WebAPI.Extensions;

/// <summary>
/// Swagger generation and the Swagger UI, including the Authorize button that lets you
/// paste a token and call the protected endpoints straight from the browser.
/// </summary>
internal static class SwaggerExtensions
{
    private const string SecuritySchemeId = "Bearer";

    public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services)
    {
        // Registered as IConfigureOptions so it is resolved after API versioning has
        // discovered the versions, which is what keeps the document list dynamic.
        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();

        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition(SecuritySchemeId, new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description =
                    "Paste only the token returned by /api/v1/auth/login. " +
                    "The 'Bearer ' prefix is added automatically."
            });

            // Microsoft.OpenApi v2 (which Swashbuckle 10 builds on) references a scheme
            // by id through OpenApiSecuritySchemeReference rather than an inline object.
            //
            // The host document MUST be passed to the reference. Without it the reference
            // cannot resolve the scheme and serialises as an empty object, which reads as
            // "this operation needs no authentication" - the Authorize button then appears
            // to work but Swagger UI never attaches the token, and every call comes back
            // 401. That is why the document is taken from the delegate instead of ignored.
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(SecuritySchemeId, document)] = []
            });

            // Includes the XML doc comments from the controllers, so the summaries written
            // against each action show up as endpoint descriptions in the UI.
            IncludeXmlComments(options);
        });

        return services;
    }

    /// <summary>
    /// Serves the UI with one dropdown entry per discovered API version.
    /// </summary>
    public static WebApplication UseSwaggerUserInterface(this WebApplication app)
    {
        app.UseSwagger();

        app.UseSwaggerUI(options =>
        {
            var descriptions = app.DescribeApiVersions();

            // Newest version first, so the UI opens on the current API.
            foreach (var description in descriptions.OrderByDescending(d => d.ApiVersion))
            {
                options.SwaggerEndpoint(
                    $"/swagger/{description.GroupName}/swagger.json",
                    $"R-Biz API {description.GroupName.ToUpperInvariant()}");
            }

            options.DocumentTitle = "R-Biz API";

            // Collapsed by default: a long endpoint list is easier to scan this way.
            options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
        });

        return app;
    }

    private static void IncludeXmlComments(SwaggerGenOptions options)
    {
        var xmlPath = Path.Combine(
            AppContext.BaseDirectory,
            $"{typeof(Program).Assembly.GetName().Name}.xml");

        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    }
}
