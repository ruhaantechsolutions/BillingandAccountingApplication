using Microsoft.EntityFrameworkCore.Metadata;
using RBiz.Domain.Entities;

namespace RBiz.Infrastructure.Persistence.Sync;

/// <summary>
/// Sync support for <see cref="User"/>.
///
/// Unlike every other synchronised entity, a user is not owned by one company - the same
/// person may work on several. Scoping therefore goes through the membership table rather
/// than a column on the row, which is why this provider supplies its own filter.
/// </summary>
internal sealed class UserSyncProvider : SyncEntityProviderBase<User>
{
    /// <summary>
    /// A device may correct a display name or deactivate someone. It may NOT change a
    /// mobile number or an email - those identify the account and their uniqueness has to
    /// be settled server-side - and it certainly may not set a password hash.
    /// </summary>
    private static readonly HashSet<string> Writable =
        new(StringComparer.OrdinalIgnoreCase) { "fullName", "isActive" };

    public UserSyncProvider(AppDbContext context, TimeProvider timeProvider)
        : base(context, timeProvider)
    {
    }

    public override string EntityName => "users";

    /// <summary>Null: there is no company column on a user. See <see cref="CompanyFilterSql"/>.</summary>
    protected override string? CompanyPropertyName => null;

    protected override IReadOnlySet<string> WritableFields => Writable;

    /// <summary>
    /// Restricts the delta to people who are active members of this company. The subquery
    /// is served by the unique index on (UserId, CompanyId) and its partner on
    /// (CompanyId, IsActive), so it stays a seek rather than a scan.
    /// </summary>
    protected override string CompanyFilterSql(IEntityType entityType) =>
        "[Id] IN (SELECT [UserId] FROM [UserCompanies] WHERE [CompanyId] = {0} AND [IsActive] = 1)";

    protected override IQueryable<User> ForCompany(IQueryable<User> source, Guid companyId) =>
        source.Where(u => u.Companies.Any(c => c.CompanyId == companyId && c.IsActive));

    /// <summary>
    /// A user is never created through sync, so there is no company to assign. Membership
    /// is granted by the company endpoints while online.
    /// </summary>
    protected override void AssignCompany(User entity, Guid companyId)
    {
    }

    /// <summary>
    /// Note what is absent: PasswordHash. A credential must never be replicated onto a
    /// device, and an offline cache is exactly the kind of place it would be forgotten.
    /// </summary>
    protected override object ToPayload(User entity) => new
    {
        entity.Id,
        entity.Mobile,
        entity.FullName,
        entity.Email,
        entity.IsActive,
        entity.CreatedAtUtc,
        entity.ModifiedAtUtc
    };

    protected override string? ApplyPayload(User entity, Dictionary<string, object?> payload, bool isNew)
    {
        if (isNew)
        {
            return "New users cannot be created through sync. Use POST /api/v1/Users while online.";
        }

        if (payload.TryGetValue("fullName", out var fullNameValue))
        {
            // Read through the helper: a JsonElement's ToString() for a string includes the
            // surrounding quotes, so naive conversion would store "\"Name\"".
            if (!SyncPayload.TryReadString(fullNameValue, out var fullName))
            {
                return "fullName cannot be empty.";
            }

            entity.FullName = fullName;
        }

        if (payload.TryGetValue("isActive", out var isActiveValue))
        {
            if (!SyncPayload.TryReadBoolean(isActiveValue, out var isActive))
            {
                return "isActive must be true or false.";
            }

            entity.IsActive = isActive;
        }

        return null;
    }
}
