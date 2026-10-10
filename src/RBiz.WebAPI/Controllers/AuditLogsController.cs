using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using RBiz.Application.Common;
using RBiz.Application.Dtos;
using RBiz.Application.Interfaces;

namespace RBiz.WebAPI.Controllers;

/// <summary>
/// Read-only view of the audit trail for the caller's tenant.
///
/// There is no POST, PUT or DELETE here by design: audit rows are written by the
/// persistence layer as a side effect of saving, and an audit trail that can be edited
/// through the API is not an audit trail.
/// </summary>
[ApiVersion("1.0")]
public sealed class AuditLogsController : ApiControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogsController(IAuditLogService auditLogService) => _auditLogService = auditLogService;

    /// <summary>
    /// Returns a page of audit entries, newest first.
    /// </summary>
    /// <param name="page">1-based page number. Defaults to 1.</param>
    /// <param name="pageSize">Rows per page. Defaults to 20, capped at 100.</param>
    /// <param name="cancellationToken">Cancelled automatically if the client disconnects.</param>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<AuditLogDto>))]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> GetAll(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var result = await _auditLogService.GetAllAsync(Paging(page, pageSize), cancellationToken);

        return Ok(result);
    }
}
