using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RBiz.Application.Common;
using RBiz.WebAPI.Extensions;

namespace RBiz.WebAPI.Controllers;

/// <summary>
/// Shared setup for every controller: the versioned route template, authentication by
/// default, and the common error response types so they appear once in Swagger instead
/// of being repeated on each action.
///
/// Authentication is opt-out rather than opt-in. A new controller is protected the moment
/// it is created; forgetting an attribute leaves an endpoint locked, not exposed.
/// </summary>
[ApiController]
[Authorize]
[Route(ApiVersioningExtensions.RouteTemplate)]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ProblemDetails))]
[ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Builds a clamped page request from the optional query-string values.
    /// </summary>
    protected static PageRequest Paging(int? page, int? pageSize) => new(page, pageSize);
}
