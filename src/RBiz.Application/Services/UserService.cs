using Microsoft.Extensions.Logging;
using RBiz.Application.Common;
using RBiz.Application.Dtos;
using RBiz.Application.Interfaces;
using RBiz.Application.Mapping;
using RBiz.Domain.Entities;
using RBiz.Domain.Exceptions;

namespace RBiz.Application.Services;

/// <inheritdoc cref="IUserService"/>
public sealed class UserService : IUserService
{
    private const int MinPasswordLength = 8;

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        ILogger<UserService> logger)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<UserDto>> GetAllAsync(
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        // The tenant comes from the validated token, never from the request, so a caller
        // cannot read another tenant's users by passing a different id.
        var tenantId = _currentUser.GetRequiredTenantId();

        var total = await _users.CountByTenantAsync(tenantId, cancellationToken);

        if (total == 0)
        {
            return new PagedResult<UserDto>([], page.Page, page.PageSize, 0);
        }

        var users = await _users.GetByTenantAsync(tenantId, page.Skip, page.PageSize, cancellationToken);

        return new PagedResult<UserDto>(
            users.ToDtoList(static u => u.ToDto()),
            page.Page,
            page.PageSize,
            total);
    }

    public async Task<UserDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.GetRequiredTenantId();

        var user = await _users.GetByIdAsync(id, tenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), id);

        return user.ToDto();
    }

    public async Task<UserDto> CreateAsync(
        CreateUserDto request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.GetRequiredTenantId();

        var email = NormaliseEmail(request.Email);
        var fullName = request.FullName?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;

        var errors = new Dictionary<string, string[]>();

        if (email.Length == 0 || !email.Contains('@'))
        {
            errors[nameof(request.Email)] = ["A valid email address is required."];
        }

        if (fullName.Length == 0)
        {
            errors[nameof(request.FullName)] = ["Full name is required."];
        }

        if (password.Length < MinPasswordLength)
        {
            errors[nameof(request.Password)] =
                [$"Password must be at least {MinPasswordLength} characters long."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        if (await _users.EmailExistsAsync(tenantId, email, cancellationToken: cancellationToken))
        {
            throw new ConflictException($"A user with email '{email}' already exists in this tenant.");
        }

        var user = new User
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Email = email,
            FullName = fullName,
            PasswordHash = _passwordHasher.Hash(password),
            IsActive = true
        };

        _users.Add(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} created in tenant {TenantId}.", user.Id, tenantId);

        return user.ToDto();
    }

    public async Task<UserDto> UpdateAsync(
        Guid id,
        UpdateUserDto request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.GetRequiredTenantId();

        var user = await _users.GetByIdAsync(id, tenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), id);

        var fullName = request.FullName?.Trim() ?? string.Empty;

        if (fullName.Length == 0)
        {
            throw ValidationException.For(nameof(request.FullName), "Full name is required.");
        }

        user.FullName = fullName;
        user.IsActive = request.IsActive;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} updated in tenant {TenantId}.", user.Id, tenantId);

        return user.ToDto();
    }

    public async Task<UserDto> UpdateEmailAsync(
        Guid id,
        UpdateUserEmailDto request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.GetRequiredTenantId();

        var user = await _users.GetByIdAsync(id, tenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), id);

        var email = NormaliseEmail(request.Email);

        if (email.Length == 0 || !email.Contains('@'))
        {
            throw ValidationException.For(nameof(request.Email), "A valid email address is required.");
        }

        // Re-submitting the address the user already has is a no-op rather than an error,
        // so a client retrying a request it never saw acknowledged is not punished for it.
        if (string.Equals(user.Email, email, StringComparison.Ordinal))
        {
            return user.ToDto();
        }

        // The user is excluded from the clash check. Without that, their own row would
        // match and a change of casing would be reported as a conflict with themselves.
        if (await _users.EmailExistsAsync(tenantId, email, id, cancellationToken))
        {
            throw new ConflictException($"A user with email '{email}' already exists in this tenant.");
        }

        var previousEmail = user.Email;
        user.Email = email;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Logged because this changes how someone signs in, which is worth being able to
        // trace. The audit trail records it as well, through the usual interceptor.
        _logger.LogInformation(
            "User {UserId} in tenant {TenantId} changed email from {PreviousEmail} to {NewEmail}.",
            user.Id,
            tenantId,
            previousEmail,
            email);

        return user.ToDto();
    }

    /// <summary>
    /// One normalisation rule for every path that accepts an email: trimmed and upper
    /// cased, matching how addresses are stored and how login looks them up.
    /// </summary>
    private static string NormaliseEmail(string? email) =>
        email?.Trim().ToUpperInvariant() ?? string.Empty;
}
