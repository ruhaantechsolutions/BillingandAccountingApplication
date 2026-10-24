using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RBiz.Application.Common;
using RBiz.Application.Interfaces;
using RBiz.Domain.Entities;

namespace RBiz.Infrastructure.Authentication;

/// <inheritdoc cref="IJwtTokenGenerator"/>
internal sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _settings;
    private readonly TimeProvider _timeProvider;

    // The signing credentials are built once and reused: deriving the key on every login
    // would allocate and hash needlessly.
    private readonly SigningCredentials _signingCredentials;

    public JwtTokenGenerator(IOptions<JwtSettings> settings, TimeProvider timeProvider)
    {
        _settings = settings.Value;
        _timeProvider = timeProvider;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public (string Token, DateTime ExpiresAtUtc) GenerateToken(User user, Guid? companyId)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresAtUtc = nowUtc.AddMinutes(_settings.ExpiryMinutes);

        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
            [JwtRegisteredClaimNames.Jti] = Guid.CreateVersion7().ToString(),
            [ClaimTypes.MobilePhone] = user.Mobile,
            [ClaimTypes.Name] = user.FullName
        };

        // Absent immediately after sign-in: the person has companies but has not opened
        // one yet. Services reading company data refuse a token without it rather than
        // falling back to some default company.
        if (companyId is { } selected)
        {
            claims[AppClaimTypes.CompanyId] = selected.ToString();
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims[JwtRegisteredClaimNames.Email] = user.Email;
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            IssuedAt = nowUtc,
            NotBefore = nowUtc,
            Expires = expiresAtUtc,
            SigningCredentials = _signingCredentials,
            Claims = claims
        };

        // JsonWebTokenHandler is the current, faster handler; the older
        // JwtSecurityTokenHandler is kept only for backwards compatibility.
        var token = new JsonWebTokenHandler().CreateToken(descriptor);

        return (token, expiresAtUtc);
    }
}
