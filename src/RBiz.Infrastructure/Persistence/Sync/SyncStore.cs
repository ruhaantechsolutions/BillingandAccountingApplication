using Microsoft.EntityFrameworkCore;
using RBiz.Application.Sync;
using RBiz.Application.Sync.Dtos;
using RBiz.Application.Sync.Interfaces;
using RBiz.Domain.Sync;

namespace RBiz.Infrastructure.Persistence.Sync;

/// <inheritdoc cref="ISyncStore"/>
internal sealed class SyncStore : ISyncStore
{
    private readonly AppDbContext _context;

    public SyncStore(AppDbContext context) => _context = context;

    /// <summary>
    /// Reads MIN_ACTIVE_ROWVERSION(), the safe upper bound for a delta.
    ///
    /// Why this is not optional: a row version is stamped when a row is written, but
    /// becomes visible only when its transaction commits. A long transaction can
    /// therefore publish a LOW row version after a client has already advanced its
    /// cursor past that value, and the client would never see that change again.
    /// MIN_ACTIVE_ROWVERSION() is the lowest row version still in flight, so capping
    /// every delta at it means no committed change can ever appear below a cursor the
    /// client already holds.
    /// </summary>
    public async Task<byte[]> GetChangeCeilingAsync(CancellationToken cancellationToken = default)
    {
        // Cast to binary(8) so the value arrives as a plain byte array rather than a
        // provider-specific rowversion type.
        var ceiling = await _context.Database
            .SqlQuery<byte[]>($"SELECT CAST(MIN_ACTIVE_ROWVERSION() AS binary(8)) AS Value")
            .FirstOrDefaultAsync(cancellationToken);

        return ceiling is { Length: SyncCursor.RowVersionLength }
            ? ceiling
            : SyncCursor.Beginning;
    }

    public Task<SyncOperation?> FindOperationAsync(
        Guid tenantId,
        Guid operationId,
        CancellationToken cancellationToken = default) =>
        _context.SyncOperations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                o => o.TenantId == tenantId && o.OperationId == operationId,
                cancellationToken);

    public void RecordOperation(
        Guid tenantId,
        Guid operationId,
        string deviceId,
        string entityName,
        Guid entityId,
        SyncResultStatus status,
        DateTime appliedAtUtc) =>
        _context.SyncOperations.Add(new SyncOperation
        {
            TenantId = tenantId,
            OperationId = operationId,
            DeviceId = deviceId,
            EntityName = entityName,
            EntityId = entityId,
            Outcome = ToOutcome(status),
            AppliedAtUtc = appliedAtUtc
        });

    private static SyncOperationOutcome ToOutcome(SyncResultStatus status) => status switch
    {
        SyncResultStatus.Applied => SyncOperationOutcome.Applied,
        SyncResultStatus.Conflict => SyncOperationOutcome.Conflict,
        _ => SyncOperationOutcome.Rejected
    };
}
