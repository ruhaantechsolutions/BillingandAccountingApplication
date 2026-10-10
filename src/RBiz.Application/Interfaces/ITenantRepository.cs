using RBiz.Domain.Entities;

namespace RBiz.Application.Interfaces;

/// <summary>
/// Persistence contract for <see cref="Tenant"/>. Declared in the Application layer so
/// business code never has to know whether the data lives in SQL Server, PostgreSQL or
/// anything else - Infrastructure supplies the implementation.
/// </summary>
public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Tenant?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tenant>> GetAllAsync(int skip, int take, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default);

    void Add(Tenant tenant);
}
