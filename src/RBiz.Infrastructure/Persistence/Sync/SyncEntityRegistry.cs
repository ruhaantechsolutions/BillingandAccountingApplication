using RBiz.Application.Sync.Interfaces;

namespace RBiz.Infrastructure.Persistence.Sync;

/// <inheritdoc cref="ISyncEntityRegistry"/>
internal sealed class SyncEntityRegistry : ISyncEntityRegistry
{
    private readonly Dictionary<string, ISyncEntityProvider> _byName;

    /// <summary>
    /// Built from whatever providers are registered in the container, so bringing a new
    /// entity into sync is one DI line and nothing here changes.
    /// </summary>
    public SyncEntityRegistry(IEnumerable<ISyncEntityProvider> providers)
    {
        Providers = [.. providers];

        _byName = Providers.ToDictionary(
            p => p.EntityName,
            StringComparer.OrdinalIgnoreCase);

        EntityNames = [.. Providers.Select(p => p.EntityName)];
    }

    public IReadOnlyList<string> EntityNames { get; }

    public IReadOnlyList<ISyncEntityProvider> Providers { get; }

    public bool TryGet(string entityName, out ISyncEntityProvider provider)
    {
        if (!string.IsNullOrWhiteSpace(entityName) &&
            _byName.TryGetValue(entityName.Trim(), out var found))
        {
            provider = found;
            return true;
        }

        provider = null!;
        return false;
    }
}
