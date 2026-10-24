using RBiz.Domain.Entities;

namespace RBiz.Application.Interfaces;

/// <summary>
/// Read-only persistence contract for the audit trail. Audit rows are written by the
/// persistence layer itself on save, so there is intentionally no Add method here:
/// nothing in business code should be able to forge or amend an audit entry.
/// </summary>
public interface IAuditLogRepository
{
    Task<IReadOnlyList<AuditLog>> GetByCompanyAsync(Guid companyId, int skip, int take, CancellationToken cancellationToken = default);

    Task<int> CountByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);
}
