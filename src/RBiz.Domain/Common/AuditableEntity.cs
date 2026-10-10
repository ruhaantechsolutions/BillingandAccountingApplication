namespace RBiz.Domain.Common;

/// <summary>
/// Base class for entities that carry an audit trail of who created or last changed them.
/// The values are stamped automatically by the persistence layer on save, so business
/// code never has to remember to set them.
/// </summary>
public abstract class AuditableEntity
{
    /// <summary>UTC timestamp of when the row was first inserted.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Identifier of the user who created the row. Null for system/seed data.</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>UTC timestamp of the most recent update. Null while the row is untouched.</summary>
    public DateTime? ModifiedAtUtc { get; set; }

    /// <summary>Identifier of the user who last updated the row.</summary>
    public Guid? ModifiedBy { get; set; }
}
