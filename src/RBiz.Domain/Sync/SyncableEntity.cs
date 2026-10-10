using RBiz.Domain.Common;

namespace RBiz.Domain.Sync;

/// <summary>
/// Base class for entities that an offline client can cache and edit. Adds row versioning
/// and soft deletion on top of the audit columns from <see cref="AuditableEntity"/>.
///
/// The setters on the sync members are deliberately non-public to outside callers: the
/// row version belongs to the database and the delete flags are set by the persistence
/// layer when a delete is saved, so business code has no reason to assign either.
/// </summary>
public abstract class SyncableEntity : AuditableEntity, ISyncable
{
    public Guid Id { get; set; }

    /// <summary>
    /// Mapped to a SQL Server rowversion column. Null until the row has been saved once.
    /// </summary>
    public byte[]? RowVersion { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public Guid? DeletedBy { get; set; }
}
