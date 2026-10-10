namespace RBiz.Domain.Sync;

/// <summary>
/// Marks an entity as participating in offline synchronisation.
///
/// An offline client needs three things from every row it caches: a stable identity it
/// can create itself, a version it can compare against the server, and a way to learn
/// that a row was deleted. This interface supplies all three.
///
/// To add a new entity to sync later (Billing, Stock, and so on): derive it from
/// <see cref="SyncableEntity"/>, call ConfigureSyncable in its EF configuration, and
/// register a provider for it. Nothing in the sync engine itself needs changing.
/// </summary>
public interface ISyncable
{
    /// <summary>
    /// Client-generatable primary key. Version 7 GUIDs are used so a device can mint ids
    /// while offline without coordinating with the server and without risking a collision.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Database-assigned row version. Strictly increasing across the whole database, which
    /// is what lets a client ask for "everything changed after this point" and lets the
    /// server detect that two devices edited the same row.
    /// </summary>
    byte[]? RowVersion { get; }

    /// <summary>
    /// Soft-delete flag. A hard delete is invisible to an offline client - the row simply
    /// stops appearing, so the client keeps its stale copy forever. Deleted rows are
    /// therefore kept as tombstones and shipped in the next delta.
    /// </summary>
    bool IsDeleted { get; }

    DateTime? DeletedAtUtc { get; }

    Guid? DeletedBy { get; }
}
