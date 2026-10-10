using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RBiz.Application.Dtos;
using RBiz.Application.Interfaces;

namespace RBiz.WebAPI.Controllers;

/// <summary>
/// Issues the access tokens every other endpoint requires.
/// </summary>
[ApiVersion("1.0")]
public sealed class AuthController : ApiControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    /// <summary>
    /// Signs in and returns a JWT access token.
    /// </summary>
    /// <remarks>
    /// Copy the accessToken from the response into the Authorize dialog at the top of this
    /// page to call the protected endpoints.
    /// </remarks>
    /// <response code="200">Credentials accepted; the response carries the token.</response>
    /// <response code="400">Credentials were missing, wrong, or the account is inactive.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuthResponseDto))]
    public async Task<ActionResult<AuthResponseDto>> Login(
        [FromBody] LoginRequestDto request,
        CancellationToken cancellationToken)
    {
        var response = await _authService.LoginAsync(request, cancellationToken);

        return Ok(response);
    }
}
