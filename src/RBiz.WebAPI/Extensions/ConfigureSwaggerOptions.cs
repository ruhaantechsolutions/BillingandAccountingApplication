using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RBiz.WebAPI.Extensions;

/// <summary>
/// Creates one Swagger document per API version that actually exists.
///
/// This is what makes the versioning dynamic end to end: the version list comes from
/// <see cref="IApiVersionDescriptionProvider"/>, which discovers versions by reflecting
/// over the [ApiVersion] attributes on the controllers. Adding a v2 controller makes a
/// "v2" entry appear in the Swagger dropdown with no change to this file or to Program.cs.
/// </summary>
internal sealed class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;

    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider) => _provider = provider;

    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in _provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, CreateVersionInfo(description));
        }
    }

    private static OpenApiInfo CreateVersionInfo(ApiVersionDescription description)
    {
        var info = new OpenApiInfo
        {
            Title = "R-Biz API",
            Version = description.ApiVersion.ToString(),
            Description = "Billing and accounting API."
        };

        if (description.IsDeprecated)
        {
            info.Description += " This version is deprecated and will be removed in a future release.";
        }

        return info;
    }
}
