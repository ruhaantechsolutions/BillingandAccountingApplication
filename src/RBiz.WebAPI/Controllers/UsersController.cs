using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using RBiz.Application.Common;
using RBiz.Application.Dtos;
using RBiz.Application.Interfaces;

namespace RBiz.WebAPI.Controllers;

/// <summary>
/// Manages the users of the caller's own tenant. The tenant is taken from the access
/// token, so none of these endpoints can reach another tenant's data.
/// </summary>
[ApiVersion("1.0")]
public sealed class UsersController : ApiControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService) => _userService = userService;

    /// <summary>
    /// Returns a page of users in the caller's tenant.
    /// </summary>
    /// <param name="page">1-based page number. Defaults to 1.</param>
    /// <param name="pageSize">Rows per page. Defaults to 20, capped at 100.</param>
    /// <param name="cancellationToken">Cancelled automatically if the client disconnects.</param>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<UserDto>))]
    public async Task<ActionResult<PagedResult<UserDto>>> GetAll(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var result = await _userService.GetAllAsync(Paging(page, pageSize), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Returns a single user from the caller's tenant.
    /// </summary>
    /// <response code="404">No such user exists in this tenant.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult<UserDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userService.GetByIdAsync(id, cancellationToken);

        return Ok(user);
    }

    /// <summary>
    /// Creates a user in the caller's tenant.
    /// </summary>
    /// <response code="201">Created; the Location header points at the new user.</response>
    /// <response code="409">That email address is already in use in this tenant.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(UserDto))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult<UserDto>> Create(
        [FromBody] CreateUserDto request,
        CancellationToken cancellationToken)
    {
        var user = await _userService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    /// <summary>
    /// Updates a user's name or active flag.
    /// </summary>
    /// <response code="404">No such user exists in this tenant.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult<UserDto>> Update(
        Guid id,
        [FromBody] UpdateUserDto request,
        CancellationToken cancellationToken)
    {
        var user = await _userService.UpdateAsync(id, request, cancellationToken);

        return Ok(user);
    }

    /// <summary>
    /// Changes a user's email address.
    /// </summary>
    /// <remarks>
    /// The email is the login identity, so it has its own endpoint rather than riding
    /// along with a general update - a display-name edit should not be able to change how
    /// somebody signs in.
    ///
    /// Online only. Uniqueness within the tenant cannot be settled on a disconnected
    /// device, which is why sync refuses to write this field.
    ///
    /// Submitting the address the user already has succeeds and changes nothing.
    /// </remarks>
    /// <response code="200">The updated user.</response>
    /// <response code="404">No such user exists in this tenant.</response>
    /// <response code="409">Another user in this tenant already uses that address.</response>
    [HttpPut("{id:guid}/email")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult<UserDto>> UpdateEmail(
        Guid id,
        [FromBody] UpdateUserEmailDto request,
        CancellationToken cancellationToken)
    {
        var user = await _userService.UpdateEmailAsync(id, request, cancellationToken);

        return Ok(user);
    }
}
