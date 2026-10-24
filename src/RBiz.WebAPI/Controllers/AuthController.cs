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
    /// Creates an account and signs it straight in.
    /// </summary>
    /// <remarks>
    /// No company is created. The response carries an empty company list, which is what
    /// sends a new account to the company registration screen.
    /// </remarks>
    /// <response code="200">Account created; the response carries both tokens.</response>
    /// <response code="400">A field is missing or malformed.</response>
    /// <response code="409">The mobile number, or the email, is already registered.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuthResponseDto))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult<AuthResponseDto>> Register(
        [FromBody] RegisterRequestDto request,
        CancellationToken cancellationToken)
    {
        var response = await _authService.RegisterAsync(request, cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Opens one of the caller's companies and returns a token scoped to it.
    /// </summary>
    /// <remarks>
    /// This is what makes switching company safe: the company travels inside the access
    /// token, so the data a request can reach changes with it rather than only the name
    /// shown on screen. Replace the stored tokens with the ones returned here.
    /// </remarks>
    /// <response code="200">A token scoped to that company.</response>
    /// <response code="404">No such company, or the caller is not a member of it.</response>
    [HttpPost("switch-company/{companyId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuthResponseDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult<AuthResponseDto>> SwitchCompany(
        Guid companyId,
        [FromQuery] string? deviceId,
        CancellationToken cancellationToken)
    {
        var response = await _authService.SwitchCompanyAsync(companyId, deviceId, cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Signs in with a mobile number and returns a JWT access token.
    /// </summary>
    /// <remarks>
    /// Copy the accessToken from the response into the Authorize dialog at the top of this
    /// page to call the protected endpoints.
    ///
    /// The response also lists every company the account can reach. An empty list means
    /// the person has none yet and should be sent to company registration; otherwise call
    /// switch-company to open one, because this token carries no company of its own.
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
