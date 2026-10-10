using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RBiz.Application.Interfaces;
using RBiz.Infrastructure.Authentication;
using RBiz.WebAPI.Authentication;

namespace RBiz.WebAPI.Extensions;

/// <summary>
/// JWT bearer validation for incoming requests. Token creation lives in Infrastructure;
/// this is only the read side.
/// </summary>
internal static class AuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtSettings.SectionName);

        var issuer = jwtSection["Issuer"];
        var audience = jwtSection["Audience"];
        var key = jwtSection["Key"];

        if (string.IsNullOrWhiteSpace(issuer) ||
            string.IsNullOrWhiteSpace(audience) ||
            string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "Jwt:Issuer, Jwt:Audience and Jwt:Key must all be configured.");
        }

        if (key.Length < JwtSettings.MinKeyLength)
        {
            throw new InvalidOperationException(
                $"Jwt:Key must be at least {JwtSettings.MinKeyLength} characters long.");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Tokens travel over TLS only. Turned off outside production so the API can
                // be run over plain HTTP locally.
                options.RequireHttpsMetadata = false;

                // Keeping the raw token out of the authentication ticket avoids holding a
                // second copy of the credential in memory for the length of the request.
                options.SaveToken = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,

                    ValidateAudience = true,
                    ValidAudience = audience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),

                    ValidateLifetime = true,

                    // The default allows five minutes of grace, meaning an expired token
                    // keeps working. For a financial API the expiry should mean what it says.
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization();

        // ICurrentUser reads the claims of the request being handled, so it needs access
        // to the HttpContext.
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        return services;
    }
}
