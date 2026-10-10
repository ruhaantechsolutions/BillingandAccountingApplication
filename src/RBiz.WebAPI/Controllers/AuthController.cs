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
    /// <response code="200">Credentials accepted; the response carries both tokens.</response>
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

    /// <summary>
    /// Exchanges a refresh token for a new access token.
    /// </summary>
    /// <remarks>
    /// This is what lets an offline PWA recover. An access token lasts an hour or two, so
    /// a device offline all day returns with a dead token and work queued on it; calling
    /// this gets a usable token back without the user retyping their password.
    ///
    /// The refresh token is ROTATED on every call: the response carries a new one and the
    /// old one stops working immediately. Always store the newest value and discard the
    /// previous one.
    ///
    /// Send only one refresh at a time. Presenting an already-rotated token is treated as
    /// a leak and signs the device out of every session descended from that login, so a
    /// client with several tabs must funnel refreshes through a single shared call.
    /// </remarks>
    /// <response code="200">A new access token and a new refresh token.</response>
    /// <response code="400">The token was unknown, expired, revoked, or from another device.</response>
    [HttpPost("refresh")]
    // Anonymous by necessity: the access token is expired by definition at this point,
    // so requiring a valid one would make the endpoint impossible to call.
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuthResponseDto))]
    public async Task<ActionResult<AuthResponseDto>> Refresh(
        [FromBody] RefreshRequestDto request,
        CancellationToken cancellationToken)
    {
        var response = await _authService.RefreshAsync(request, cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Signs out by revoking the refresh token and every token from the same login.
    /// </summary>
    /// <remarks>
    /// Returns 204 whether or not the token was still valid, so a client retrying a
    /// logout is never told something went wrong. Any access token already issued stays
    /// valid until it expires on its own - that is the nature of a stateless token.
    /// </remarks>
    /// <response code="204">The session is revoked, or was already gone.</response>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequestDto request,
        CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(request, cancellationToken);

        return NoContent();
    }
}
