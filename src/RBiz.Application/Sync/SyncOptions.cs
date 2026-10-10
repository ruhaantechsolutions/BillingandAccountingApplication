namespace RBiz.Application.Sync;

/// <summary>
/// Limits on sync traffic, bound from the "Sync" configuration section.
///
/// Both caps exist to stop a single device from monopolising the server: a PWA that has
/// been offline for a month would otherwise ask for its entire history in one request.
/// </summary>
public sealed class SyncOptions
{
    public const string SectionName = "Sync";

    /// <summary>Rows per entity in one pull. A client that wants more pages again.</summary>
    public int MaxPullPageSize { get; init; } = 500;

    /// <summary>Default when the client does not ask for a specific page size.</summary>
    public int DefaultPullPageSize { get; init; } = 200;

    /// <summary>Changes accepted in one push batch.</summary>
    public int MaxPushBatchSize { get; init; } = 200;
}
