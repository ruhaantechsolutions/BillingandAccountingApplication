using RBiz.Application.Common;
using RBiz.Application.Dtos;

namespace RBiz.Application.Interfaces;

/// <summary>Tenant use cases.</summary>
public interface ITenantService
{
    Task<PagedResult<TenantDto>> GetAllAsync(PageRequest page, CancellationToken cancellationToken = default);

    Task<TenantDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TenantDto> CreateAsync(CreateTenantDto request, CancellationToken cancellationToken = default);

    Task<TenantDto> UpdateAsync(Guid id, UpdateTenantDto request, CancellationToken cancellationToken = default);
}
