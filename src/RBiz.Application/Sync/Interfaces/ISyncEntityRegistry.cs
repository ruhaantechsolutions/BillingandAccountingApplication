using RBiz.Application.Sync.Interfaces;

namespace RBiz.Application.Sync.Interfaces;

/// <summary>
/// Looks up the registered providers. Resolving by name keeps the controller free of any
/// knowledge of which entities exist.
/// </summary>
public interface ISyncEntityRegistry
{
    /// <summary>Names of every entity currently participating in sync.</summary>
    IReadOnlyList<string> EntityNames { get; }

    /// <summary>All registered providers, in registration order.</summary>
    IReadOnlyList<ISyncEntityProvider> Providers { get; }

    /// <summary>
    /// Finds a provider by entity name, case-insensitively. Returns false for an unknown
    /// name rather than throwing, so one bad item in a batch is a rejection, not a 500.
    /// </summary>
    bool TryGet(string entityName, out ISyncEntityProvider provider);
}
