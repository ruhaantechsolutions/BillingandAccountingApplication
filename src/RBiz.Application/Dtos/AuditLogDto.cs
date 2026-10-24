using RBiz.Domain.Entities;

namespace RBiz.Application.Dtos;

/// <summary>A single audit-trail entry as returned by the API.</summary>
public sealed record AuditLogDto(
    long Id,
    Guid? CompanyId,
    Guid? UserId,
    AuditAction Action,
    string EntityName,
    string EntityId,
    string? OldValues,
    string? NewValues,
    DateTime TimestampUtc);
