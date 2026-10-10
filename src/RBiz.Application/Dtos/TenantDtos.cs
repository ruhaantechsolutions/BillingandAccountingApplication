namespace RBiz.Application.Dtos;

/// <summary>Tenant as returned by the API.</summary>
public sealed record TenantDto(
    Guid Id,
    string Name,
    string Code,
    bool IsActive,
    DateTime CreatedAtUtc);

/// <summary>Payload for creating a tenant.</summary>
public sealed record CreateTenantDto(string Name, string Code);

/// <summary>Payload for updating a tenant.</summary>
public sealed record UpdateTenantDto(string Name, bool IsActive);
