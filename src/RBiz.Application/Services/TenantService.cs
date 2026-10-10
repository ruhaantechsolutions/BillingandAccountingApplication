using Microsoft.Extensions.Logging;
using RBiz.Application.Common;
using RBiz.Application.Dtos;
using RBiz.Application.Interfaces;
using RBiz.Application.Mapping;
using RBiz.Domain.Entities;
using RBiz.Domain.Exceptions;

namespace RBiz.Application.Services;

/// <inheritdoc cref="ITenantService"/>
public sealed class TenantService : ITenantService
{
    private readonly ITenantRepository _tenants;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TenantService> _logger;

    public TenantService(
        ITenantRepository tenants,
        IUnitOfWork unitOfWork,
        ILogger<TenantService> logger)
    {
        _tenants = tenants;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<TenantDto>> GetAllAsync(
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        var total = await _tenants.CountAsync(cancellationToken);

        // Skip the second round trip entirely when there is nothing to fetch.
        if (total == 0)
        {
            return new PagedResult<TenantDto>([], page.Page, page.PageSize, 0);
        }

        var tenants = await _tenants.GetAllAsync(page.Skip, page.PageSize, cancellationToken);

        return new PagedResult<TenantDto>(
            tenants.ToDtoList(static t => t.ToDto()),
            page.Page,
            page.PageSize,
            total);
    }

    public async Task<TenantDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenants.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), id);

        return tenant.ToDto();
    }

    public async Task<TenantDto> CreateAsync(
        CreateTenantDto request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        var code = request.Code?.Trim().ToUpperInvariant() ?? string.Empty;

        var errors = new Dictionary<string, string[]>();

        if (name.Length == 0)
        {
            errors[nameof(request.Name)] = ["Tenant name is required."];
        }

        if (code.Length == 0)
        {
            errors[nameof(request.Code)] = ["Tenant code is required."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        if (await _tenants.CodeExistsAsync(code, cancellationToken))
        {
            throw new ConflictException($"A tenant with code '{code}' already exists.");
        }

        var tenant = new Tenant
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Code = code,
            IsActive = true
        };

        _tenants.Add(tenant);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Tenant {TenantId} created with code {Code}.", tenant.Id, tenant.Code);

        return tenant.ToDto();
    }

    public async Task<TenantDto> UpdateAsync(
        Guid id,
        UpdateTenantDto request,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenants.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), id);

        var name = request.Name?.Trim() ?? string.Empty;

        if (name.Length == 0)
        {
            throw ValidationException.For(nameof(request.Name), "Tenant name is required.");
        }

        tenant.Name = name;
        tenant.IsActive = request.IsActive;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Tenant {TenantId} updated.", tenant.Id);

        return tenant.ToDto();
    }
}
