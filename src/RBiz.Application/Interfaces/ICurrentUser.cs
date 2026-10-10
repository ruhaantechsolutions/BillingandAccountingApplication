namespace RBiz.Application.Interfaces;

/// <summary>
/// The user behind the current request, read from the validated JWT. Lets services and
/// the audit trail know who is acting without the Application layer touching HttpContext.
/// All properties are null for unauthenticated calls.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    Guid? TenantId { get; }

    string? Email { get; }

    bool IsAuthenticated { get; }

    /// <summary>
    /// Returns the tenant of the caller, or throws when the request is not authenticated.
    /// Used by services that must never run outside a tenant context.
    /// </summary>
    Guid GetRequiredTenantId();
}
