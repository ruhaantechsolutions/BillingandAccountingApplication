using Microsoft.Extensions.Options;
using RBiz.Application.Interfaces;

namespace RBiz.Infrastructure.Authentication;

/// <inheritdoc cref="IRefreshTokenLifetime"/>
internal sealed class ConfiguredRefreshTokenLifetime : IRefreshTokenLifetime
{
    private readonly IOptions<JwtSettings> _settings;

    public ConfiguredRefreshTokenLifetime(IOptions<JwtSettings> settings) => _settings = settings;

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_settings.Value.RefreshTokenDays);
}
