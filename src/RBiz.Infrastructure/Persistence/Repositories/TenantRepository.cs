using Microsoft.EntityFrameworkCore;
using RBiz.Application.Interfaces;
using RBiz.Domain.Entities;

namespace RBiz.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="ITenantRepository"/>
internal sealed class TenantRepository : ITenantRepository
{
    private readonly AppDbContext _context;

    public TenantRepository(AppDbContext context) => _context = context;

    /// <summary>
    /// Tracked, because callers of this overload go on to modify the tenant and save.
    /// </summary>
    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<Tenant?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        _context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Code == code, cancellationToken);

    /// <summary>
    /// Read-only listing: no change tracking, so EF skips building snapshots for rows
    /// that will only ever be serialised to JSON.
    /// </summary>
    public async Task<IReadOnlyList<Tenant>> GetAllAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default) =>
        await _context.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        _context.Tenants.CountAsync(cancellationToken);

    /// <summary>
    /// Existence check that asks the database for a boolean rather than fetching the row.
    /// </summary>
    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default) =>
        _context.Tenants.AnyAsync(t => t.Code == code, cancellationToken);

    public void Add(Tenant tenant) => _context.Tenants.Add(tenant);
}
