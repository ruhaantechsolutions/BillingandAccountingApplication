using RBiz.Application.Common;
using RBiz.Application.Dtos;

namespace RBiz.Application.Interfaces;

/// <summary>Read-only access to the audit trail for the caller's tenant.</summary>
public interface IAuditLogService
{
    Task<PagedResult<AuditLogDto>> GetAllAsync(PageRequest page, CancellationToken cancellationToken = default);
}
