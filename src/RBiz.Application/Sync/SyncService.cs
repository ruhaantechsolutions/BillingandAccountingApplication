using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RBiz.Application.Interfaces;
using RBiz.Application.Sync.Dtos;
using RBiz.Application.Sync.Interfaces;
using RBiz.Domain.Exceptions;

namespace RBiz.Application.Sync;

/// <inheritdoc cref="ISyncService"/>
public sealed class SyncService : ISyncService
{
    private readonly ISyncEntityRegistry _registry;
    private readonly ISyncStore _store;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly SyncOptions _options;
    private readonly ILogger<SyncService> _logger;

    public SyncService(
        ISyncEntityRegistry registry,
        ISyncStore store,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        IOptions<SyncOptions> options,
        ILogger<SyncService> logger)
    {
        _registry = registry;
        _store = store;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SyncPullResponse> PullAsync(
        string? cursor,
        IReadOnlyList<string>? entities,
        int? limit,
        CancellationToken cancellationToken = default)
    {
        // Always the caller's own company, from the validated token.
        var companyId = _currentUser.GetRequiredCompanyId();

        if (!SyncCursor.TryDecode(cursor, out var afterRowVersion))
        {
            // Rejected rather than quietly restarting from zero, which would make a
            // client silently re-download everything and look like a server fault.
            throw ValidationException.For(
                nameof(cursor),
                "The sync cursor is not valid. Omit it to start a full synchronisation.");
        }

        var pageSize = ResolvePageSize(limit);

        // Resolved before any database work, so a bad entity name is answered as a 400
        // without costing a round trip.
        var providers = ResolveProviders(entities);

        // Everything in this response is bounded by one ceiling, so the cursor the client
        // stores is safe for every entity in it.
        var ceiling = await _store.GetChangeCeilingAsync(cancellationToken);
        var changeSets = new List<SyncEntityChangeSet>(providers.Count);

        foreach (var provider in providers)
        {
            var changeSet = await provider.GetChangesAsync(
                companyId,
                afterRowVersion,
                ceiling,
                pageSize,
                cancellationToken);

            changeSets.Add(changeSet);
        }

        _logger.LogInformation(
            "Sync pull for company {CompanyId} returned {ChangeCount} changes across {EntityCount} entities.",
            companyId,
            changeSets.Sum(c => c.Changes.Count),
            changeSets.Count);

        return new SyncPullResponse(changeSets, _timeProvider.GetUtcNow().UtcDateTime);
    }

    public async Task<SyncPushResponse> PushAsync(
        SyncPushRequest request,
        CancellationToken cancellationToken = default)
    {
        var companyId = _currentUser.GetRequiredCompanyId();

        ValidatePushRequest(request);

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var results = new List<SyncPushItemResult>(request.Items.Count);

        foreach (var item in request.Items)
        {
            var result = await ApplyItemAsync(companyId, request.DeviceId, item, nowUtc, cancellationToken);
            results.Add(result);
        }

        // One commit for the whole batch: the changes and their ledger entries land
        // together, so a crash cannot leave an operation marked applied that is not.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var response = new SyncPushResponse(results, nowUtc);

        _logger.LogInformation(
            "Sync push from device {DeviceId} for company {CompanyId}: {Applied} applied, " +
            "{Conflicts} conflicts, {Rejected} rejected.",
            request.DeviceId,
            companyId,
            response.AppliedCount,
            response.ConflictCount,
            response.RejectedCount);

        return response;
    }

    public SyncStatusResponse GetStatus() => new(
        _timeProvider.GetUtcNow().UtcDateTime,
        _registry.EntityNames,
        _options.MaxPullPageSize,
        _options.MaxPushBatchSize);

    /// <summary>
    /// Handles one pushed change, short-circuiting anything already seen.
    /// </summary>
    private async Task<SyncPushItemResult> ApplyItemAsync(
        Guid companyId,
        string deviceId,
        SyncPushItem item,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (!_registry.TryGet(item.Entity, out var provider))
        {
            return Rejected(item, $"'{item.Entity}' is not a synchronised entity.");
        }

        if (item.OperationId == Guid.Empty)
        {
            return Rejected(item, "operationId is required so a retry can be recognised.");
        }

        // A retry of something already handled returns the original answer instead of
        // applying it a second time.
        var existing = await _store.FindOperationAsync(companyId, item.OperationId, cancellationToken);

        if (existing is not null)
        {
            return new SyncPushItemResult(
                item.OperationId,
                item.Id,
                item.Entity,
                SyncResultStatus.Duplicate,
                null,
                "This change was already received and is not applied twice.",
                null);
        }

        var result = await provider.ApplyAsync(companyId, item, cancellationToken);

        _store.RecordOperation(
            companyId,
            item.OperationId,
            deviceId,
            item.Entity,
            item.Id,
            result.Status,
            nowUtc);

        return result;
    }

    private void ValidatePushRequest(SyncPushRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            errors[nameof(request.DeviceId)] = ["deviceId is required."];
        }

        if (request.Items.Count == 0)
        {
            errors[nameof(request.Items)] = ["At least one change is required."];
        }
        else if (request.Items.Count > _options.MaxPushBatchSize)
        {
            errors[nameof(request.Items)] =
                [$"A batch may contain at most {_options.MaxPushBatchSize} changes."];
        }
        else
        {
            // A repeated operation id inside one batch cannot be deduplicated by the
            // ledger, because neither copy has been committed yet.
            var duplicates = request.Items
                .GroupBy(i => i.OperationId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToArray();

            if (duplicates.Length > 0)
            {
                errors[nameof(request.Items)] =
                    [$"operationId must be unique within a batch. Repeated: {string.Join(", ", duplicates)}."];
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    /// <summary>
    /// Narrows the pull to the entities the client asked for, or all of them by default.
    /// An unknown name is an error, not silence: a client that misspells an entity would
    /// otherwise believe that entity simply never changes.
    /// </summary>
    private List<ISyncEntityProvider> ResolveProviders(IReadOnlyList<string>? entities)
    {
        if (entities is null || entities.Count == 0)
        {
            return [.. _registry.Providers];
        }

        var resolved = new List<ISyncEntityProvider>(entities.Count);
        var unknown = new List<string>();

        foreach (var name in entities)
        {
            if (_registry.TryGet(name, out var provider))
            {
                if (!resolved.Contains(provider))
                {
                    resolved.Add(provider);
                }
            }
            else
            {
                unknown.Add(name);
            }
        }

        if (unknown.Count > 0)
        {
            throw ValidationException.For(
                "entities",
                $"Unknown entity: {string.Join(", ", unknown)}. " +
                $"Known entities are {string.Join(", ", _registry.EntityNames)}.");
        }

        return resolved;
    }

    private int ResolvePageSize(int? limit) => limit switch
    {
        null or <= 0 => _options.DefaultPullPageSize,
        _ => Math.Min(limit.Value, _options.MaxPullPageSize)
    };

    private static SyncPushItemResult Rejected(SyncPushItem item, string message) => new(
        item.OperationId,
        item.Id,
        item.Entity,
        SyncResultStatus.Rejected,
        null,
        message,
        null);
}
