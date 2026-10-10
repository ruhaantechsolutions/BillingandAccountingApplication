namespace RBiz.Application.Sync.Interfaces;

using RBiz.Application.Sync.Dtos;
using RBiz.Domain.Sync;

/// <summary>
/// The persistence-level pieces of sync that are not specific to any one entity: the
/// change-stream ceiling and the idempotency ledger.
/// </summary>
public interface ISyncStore
{
    /// <summary>
    /// The highest row version it is safe to hand a client as a cursor.
    ///
    /// On SQL Server this is MIN_ACTIVE_ROWVERSION(): the lowest row version still in use
    /// by an open transaction. Reading past it risks skipping a change that is committed
    /// after the client has already moved its cursor beyond that value.
    /// </summary>
    Task<byte[]> GetChangeCeilingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the outcome recorded for an operation the client has sent before, or null
    /// if this is the first time it has been seen.
    /// </summary>
    Task<SyncOperation?> FindOperationAsync(
        Guid tenantId,
        Guid operationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that an operation was handled. Written in the same transaction as the
    /// change itself, so the ledger can never claim something the data does not show.
    /// </summary>
    void RecordOperation(
        Guid tenantId,
        Guid operationId,
        string deviceId,
        string entityName,
        Guid entityId,
        SyncResultStatus status,
        DateTime appliedAtUtc);
}
