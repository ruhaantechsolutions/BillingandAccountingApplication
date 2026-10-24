using RBiz.Domain.Entities;

namespace RBiz.Infrastructure.Persistence.Sync;

/// <summary>
/// Sync support for <see cref="Company"/>, so a device knows the business it is billing
/// for while offline.
///
/// A device only ever receives the company it is working on: the company key for this
/// entity is its own primary key.
/// </summary>
internal sealed class CompanySyncProvider : SyncEntityProviderBase<Company>
{
    /// <summary>
    /// Empty: creating or renaming a business is an administrative act that belongs
    /// online, where the GST profile and duplicate checks can be settled properly.
    /// </summary>
    private static readonly HashSet<string> Writable = new(StringComparer.OrdinalIgnoreCase);

    public CompanySyncProvider(AppDbContext context, TimeProvider timeProvider)
        : base(context, timeProvider)
    {
    }

    public override string EntityName => "companies";

    protected override string? CompanyPropertyName => nameof(Company.Id);

    protected override IReadOnlySet<string> WritableFields => Writable;

    protected override IQueryable<Company> ForCompany(IQueryable<Company> source, Guid companyId) =>
        source.Where(c => c.Id == companyId);

    protected override void AssignCompany(Company entity, Guid companyId) => entity.Id = companyId;

    protected override object ToPayload(Company entity) => new
    {
        entity.Id,
        entity.Name,
        entity.GstStatus,
        entity.IsActive,
        entity.CreatedAtUtc,
        entity.ModifiedAtUtc
    };

    /// <summary>
    /// Unreachable in practice: an empty <see cref="WritableFields"/> means the base class
    /// rejects every payload first. Kept so the entity cannot become writable by accident.
    /// </summary>
    protected override string? ApplyPayload(Company entity, Dictionary<string, object?> payload, bool isNew) =>
        "Companies are read-only over sync. Use the company endpoints while online.";
}
