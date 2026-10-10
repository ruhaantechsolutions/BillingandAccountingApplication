using RBiz.Application.Sync.Dtos;

namespace RBiz.Application.Sync.Interfaces;

/// <summary>
/// The two operations an offline client needs: pull what changed, push what it did.
/// </summary>
public interface ISyncService
{
    /// <summary>
    /// Returns everything that changed in the caller's tenant after the given cursor.
    /// A null or blank cursor means a first full download.
    /// </summary>
    Task<SyncPullResponse> PullAsync(
        string? cursor,
        IReadOnlyList<string>? entities,
        int? limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a batch of client changes and reports the outcome of each one.
    /// </summary>
    Task<SyncPushResponse> PushAsync(
        SyncPushRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Server time and sync limits, for a client deciding whether it is online.</summary>
    SyncStatusResponse GetStatus();
}
