using RBiz.Application.Dtos;

namespace RBiz.Application.Interfaces;

/// <summary>Authentication use cases.</summary>
public interface IAuthService
{
    /// <summary>
    /// Verifies credentials and issues an access token.
    /// </summary>
    /// <exception cref="Domain.Exceptions.ValidationException">
    /// Thrown when the credentials are wrong or the account is unavailable.
    /// </exception>
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
}
