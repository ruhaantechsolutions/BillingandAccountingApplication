namespace RBiz.Domain.Sync;

/// <summary>
/// Record of one change already accepted from a client device, kept so that a replay of
/// the same change is recognised instead of applied twice.
///
/// This matters more offline than anywhere else: a device on a weak connection will
/// retry a push it never saw acknowledged, and without this ledger a retried batch
/// would duplicate every row in it.
/// </summary>
public class SyncOperation
{
    public long Id { get; set; }

    /// <summary>Company the operation belonged to. Part of the uniqueness rule.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>
    /// Identifier the client generated for this change and reuses on every retry.
    /// Unique per tenant, which is what makes the push endpoint idempotent.
    /// </summary>
    public Guid OperationId { get; set; }

    /// <summary>Device that submitted it, for diagnosing a misbehaving client.</summary>
    public string DeviceId { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    /// <summary>
    /// The outcome recorded the first time round, replayed verbatim on a retry so the
    /// client sees a consistent answer.
    /// </summary>
    public SyncOperationOutcome Outcome { get; set; }

    public DateTime AppliedAtUtc { get; set; }
}

/// <summary>What happened to a pushed change.</summary>
public enum SyncOperationOutcome
{
    Applied = 1,
    Conflict = 2,
    Rejected = 3
}
