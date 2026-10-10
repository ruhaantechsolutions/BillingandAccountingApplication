using RBiz.Application.Sync.Dtos;

namespace RBiz.Application.Sync.Interfaces;

/// <summary>
/// Everything the sync engine needs to know about one entity type.
///
/// THIS IS THE EXTENSION POINT. To bring a new entity (Billing, Stock, ...) into sync:
///   1. derive the entity from SyncableEntity,
///   2. call builder.ConfigureSyncable() in its EF configuration,
///   3. add a provider deriving from SyncEntityProviderBase and register it in DI.
/// The sync engine, the controller and the cursor logic stay untouched.
/// </summary>
public interface ISyncEntityProvider
{
    /// <summary>
    /// Name the client uses for this entity, lower-case and plural, e.g. "users".
    /// Part of the wire contract, so it must stay stable once a client ships.
    /// </summary>
    string EntityName { get; }

    /// <summary>
    /// Returns rows changed after <paramref name="afterRowVersion"/> and at or below
    /// <paramref name="ceilingRowVersion"/>, oldest first, at most <paramref name="limit"/>.
    ///
    /// The ceiling is not optional. It is what prevents a change committed by a
    /// long-running transaction from being skipped forever.
    /// </summary>
    Task<SyncEntityChangeSet> GetChangesAsync(
        Guid tenantId,
        byte[] afterRowVersion,
        byte[] ceilingRowVersion,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies one client change. Must not call SaveChanges: the engine commits the whole
    /// batch together so a partially-applied push cannot be left behind.
    /// </summary>
    Task<SyncPushItemResult> ApplyAsync(
        Guid tenantId,
        SyncPushItem item,
        CancellationToken cancellationToken = default);
}
