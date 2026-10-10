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

    public (string Token, DateTime ExpiresAtUtc) GenerateToken(User user)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresAtUtc = nowUtc.AddMinutes(_settings.ExpiryMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            IssuedAt = nowUtc,
            NotBefore = nowUtc,
            Expires = expiresAtUtc,
            SigningCredentials = _signingCredentials,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.CreateVersion7().ToString(),
                [JwtRegisteredClaimNames.Email] = user.Email,
                [ClaimTypes.Name] = user.FullName,
                [AppClaimTypes.TenantId] = user.TenantId.ToString()
            }
        };

        // JsonWebTokenHandler is the current, faster handler; the older
        // JwtSecurityTokenHandler is kept only for backwards compatibility.
        var token = new JsonWebTokenHandler().CreateToken(descriptor);

        return (token, expiresAtUtc);
    }
}
