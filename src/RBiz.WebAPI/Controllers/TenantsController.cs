using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using RBiz.Application.Common;
using RBiz.Application.Dtos;
using RBiz.Application.Interfaces;

namespace RBiz.WebAPI.Controllers;

/// <summary>
/// Manages the businesses that use the system.
/// </summary>
[ApiVersion("1.0")]
public sealed class TenantsController : ApiControllerBase
{
    private readonly ITenantService _tenantService;

    public TenantsController(ITenantService tenantService) => _tenantService = tenantService;

    /// <summary>
    /// Returns a page of tenants.
    /// </summary>
    /// <param name="page">1-based page number. Defaults to 1.</param>
    /// <param name="pageSize">Rows per page. Defaults to 20, capped at 100.</param>
    /// <param name="cancellationToken">Cancelled automatically if the client disconnects.</param>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<TenantDto>))]
    public async Task<ActionResult<PagedResult<TenantDto>>> GetAll(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var result = await _tenantService.GetAllAsync(Paging(page, pageSize), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Returns a single tenant by id.
    /// </summary>
    /// <response code="404">No tenant exists with that id.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TenantDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult<TenantDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var tenant = await _tenantService.GetByIdAsync(id, cancellationToken);

        return Ok(tenant);
    }

    /// <summary>
    /// Creates a tenant.
    /// </summary>
    /// <response code="201">Created; the Location header points at the new tenant.</response>
    /// <response code="409">A tenant with that code already exists.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(TenantDto))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult<TenantDto>> Create(
        [FromBody] CreateTenantDto request,
        CancellationToken cancellationToken)
    {
        var tenant = await _tenantService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, tenant);
    }

    /// <summary>
    /// Updates a tenant's name or active flag.
    /// </summary>
    /// <response code="404">No tenant exists with that id.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TenantDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult<TenantDto>> Update(
        Guid id,
        [FromBody] UpdateTenantDto request,
        CancellationToken cancellationToken)
    {
        var tenant = await _tenantService.UpdateAsync(id, request, cancellationToken);

        return Ok(tenant);
    }
}
