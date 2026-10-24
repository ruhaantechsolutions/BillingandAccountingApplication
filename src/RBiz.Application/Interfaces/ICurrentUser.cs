namespace RBiz.Application.Interfaces;

/// <summary>
/// The user behind the current request, read from the validated JWT. Lets services and
/// the audit trail know who is acting without the Application layer touching HttpContext.
/// All properties are null for unauthenticated calls.
///
/// <see cref="UserId"/> is present for any signed-in caller. <see cref="CompanyId"/> is
/// present only once a company has been selected.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    Guid? CompanyId { get; }

    string? Email { get; }

    bool IsAuthenticated { get; }

    /// <summary>
    /// The company the caller is working on, or throws when the token carries none.
    ///
    /// A token issued by sign-in has no company yet, so any service reading company data
    /// uses this and fails closed rather than quietly operating on nothing.
    /// </summary>
    Guid GetRequiredCompanyId();
}
