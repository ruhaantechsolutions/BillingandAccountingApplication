using RBiz.Application.Common;
using RBiz.Application.Dtos;
using RBiz.Application.Interfaces;
using RBiz.Application.Mapping;

namespace RBiz.Application.Services;

/// <inheritdoc cref="IAuditLogService"/>
public sealed class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _auditLogs;
    private readonly ICurrentUser _currentUser;

    public AuditLogService(IAuditLogRepository auditLogs, ICurrentUser currentUser)
    {
        _auditLogs = auditLogs;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<AuditLogDto>> GetAllAsync(
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.GetRequiredTenantId();

        var total = await _auditLogs.CountByTenantAsync(tenantId, cancellationToken);

        if (total == 0)
        {
            return new PagedResult<AuditLogDto>([], page.Page, page.PageSize, 0);
        }

        var logs = await _auditLogs.GetByTenantAsync(tenantId, page.Skip, page.PageSize, cancellationToken);

        return new PagedResult<AuditLogDto>(
            logs.ToDtoList(static l => l.ToDto()),
            page.Page,
            page.PageSize,
            total);
    }
}
