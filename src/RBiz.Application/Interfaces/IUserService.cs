using RBiz.Application.Common;
using RBiz.Application.Dtos;

namespace RBiz.Application.Interfaces;

/// <summary>
/// User use cases. Every operation is scoped to the caller's tenant, taken from the
/// access token rather than from the request body.
/// </summary>
public interface IUserService
{
    Task<PagedResult<UserDto>> GetAllAsync(PageRequest page, CancellationToken cancellationToken = default);

    Task<UserDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UserDto> CreateAsync(CreateUserDto request, CancellationToken cancellationToken = default);

    Task<UserDto> UpdateAsync(Guid id, UpdateUserDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes a user's email, which is their login identity.
    ///
    /// Enforced online only: uniqueness within the tenant cannot be settled on a device,
    /// which is why sync refuses to write this field.
    /// </summary>
    /// <exception cref="Domain.Exceptions.ConflictException">
    /// Thrown when another user in the tenant already uses that address.
    /// </exception>
    Task<UserDto> UpdateEmailAsync(
        Guid id,
        UpdateUserEmailDto request,
        CancellationToken cancellationToken = default);
}
