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
    public static AddressDto ToDto(this CompanyAddress address) => new(
        address.Line1,
        address.Line2,
        address.City,
        address.State,
        address.PinCode,
        address.Country);

    public static CompanySummaryDto ToSummaryDto(this Company company, CompanyRole role) => new(
        company.Id,
        company.Name,
        company.Address?.City ?? string.Empty,
        company.Address?.State ?? string.Empty,
        company.GstStatus,
        role);

    public static CompanyDto ToDto(this Company company, CompanyRole role) => new(
        company.Id,
        company.Name,
        company.GstStatus,
        company.IsActive,
        company.Address?.ToDto() ?? new AddressDto(string.Empty, null, string.Empty, string.Empty, string.Empty, "India"),
        company.GstRegistration?.Gstin,
        company.GstRegistration?.RegistrationType,
        company.GstRegistration?.VerificationStatus,
        role,
        company.CreatedAtUtc);

    public static UserDto ToDto(this User user) => new(
        user.Id,
        user.Mobile,
        user.FullName,
        user.Email,
        user.IsActive,
        user.CreatedAtUtc);

    public static AuditLogDto ToDto(this AuditLog log) => new(
        log.Id,
        log.CompanyId,
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
