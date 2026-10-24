using Microsoft.EntityFrameworkCore;
using RBiz.Application.Interfaces;
using RBiz.Domain.Entities;

namespace RBiz.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IAuditLogRepository"/>
internal sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _context;

    public AuditLogRepository(AppDbContext context) => _context = context;

    /// <summary>
    /// Newest first, which is both the useful order for an audit screen and the order the
    /// covering index on (TenantId, TimestampUtc DESC) already provides.
    /// </summary>
    public async Task<IReadOnlyList<AuditLog>> GetByCompanyAsync(
        Guid companyId,
        int skip,
        int take,
        CancellationToken cancellationToken = default) =>
        await _context.AuditLogs
            .AsNoTracking()
            .Where(a => a.CompanyId == companyId)
            .OrderByDescending(a => a.TimestampUtc)
            .ThenByDescending(a => a.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<int> CountByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        _context.AuditLogs.CountAsync(a => a.CompanyId == companyId, cancellationToken);
}
