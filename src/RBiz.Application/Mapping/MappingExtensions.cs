using RBiz.Application.Dtos;
using RBiz.Domain.Entities;

namespace RBiz.Application.Mapping;

/// <summary>
/// Entity-to-DTO mapping, written by hand on purpose. A convention-based mapper would
/// add a dependency, reflection cost at runtime and a class of silent bugs when a
/// property is renamed; these methods are compiled, allocation-light and greppable.
/// </summary>
public static class MappingExtensions
{
    public static TenantDto ToDto(this Tenant tenant) => new(
        tenant.Id,
        tenant.Name,
        tenant.Code,
        tenant.IsActive,
        tenant.CreatedAtUtc);

    public static UserDto ToDto(this User user) => new(
        user.Id,
        user.TenantId,
        user.Email,
        user.FullName,
        user.IsActive,
        user.CreatedAtUtc);

    public static AuditLogDto ToDto(this AuditLog log) => new(
        log.Id,
        log.TenantId,
        log.UserId,
        log.Action,
        log.EntityName,
        log.EntityId,
        log.OldValues,
        log.NewValues,
        log.TimestampUtc);

    /// <summary>
    /// Maps a list in one pass into an exactly-sized array, avoiding the repeated
    /// re-allocation a growing List would cause.
    /// </summary>
    public static IReadOnlyList<TDto> ToDtoList<TEntity, TDto>(
        this IReadOnlyList<TEntity> source,
        Func<TEntity, TDto> map)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var result = new TDto[source.Count];
        for (var i = 0; i < source.Count; i++)
        {
            result[i] = map(source[i]);
        }

        return result;
    }
}
