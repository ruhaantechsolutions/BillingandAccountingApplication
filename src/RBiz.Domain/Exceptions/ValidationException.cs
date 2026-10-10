namespace RBiz.Domain.Exceptions;

/// <summary>
/// Thrown when incoming data breaks a business rule. Surfaces as HTTP 400 together with
/// the per-field <see cref="Errors"/> dictionary, so a client can highlight the exact inputs.
/// </summary>
public sealed class ValidationException : DomainException
{
    private const string DefaultMessage = "One or more validation errors occurred.";

    public ValidationException(string message) : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IDictionary<string, string[]> errors) : base(DefaultMessage)
    {
        Errors = errors;
    }

    /// <summary>
    /// Field name to the list of problems with that field.
    /// </summary>
    public IDictionary<string, string[]> Errors { get; }

    /// <summary>
    /// Convenience factory for the common single-field case.
    /// </summary>
    public static ValidationException For(string field, string error) =>
        new(new Dictionary<string, string[]> { [field] = [error] });
}
