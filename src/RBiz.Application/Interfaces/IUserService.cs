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
}
