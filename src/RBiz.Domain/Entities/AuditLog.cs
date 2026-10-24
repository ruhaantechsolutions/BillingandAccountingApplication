namespace RBiz.Domain.Entities;

/// <summary>
/// What kind of change produced an <see cref="AuditLog"/> row.
/// </summary>
public enum AuditAction
{
    Created = 1,
    Updated = 2,
    Deleted = 3
}

/// <summary>
/// An append-only record of a single change to a tracked entity. Written automatically
/// by the persistence layer, never updated or deleted, which is what makes it usable as
/// evidence in an accounting context.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    /// <summary>Company the change belonged to. Null for system-level changes.</summary>
    public Guid? CompanyId { get; set; }

    /// <summary>User who made the change. Null when performed by the system.</summary>
    public Guid? UserId { get; set; }

    public AuditAction Action { get; set; }

    /// <summary>CLR name of the changed entity, e.g. "User".</summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>Primary key of the changed row, serialised as text.</summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>JSON of the changed columns before the change. Null on insert.</summary>
    public string? OldValues { get; set; }

    /// <summary>JSON of the changed columns after the change. Null on delete.</summary>
    public string? NewValues { get; set; }

    public DateTime TimestampUtc { get; set; }
}
