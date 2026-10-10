namespace RBiz.Domain.Exceptions;

/// <summary>
/// Thrown when a request clashes with the current state of the data, such as reusing an
/// email that already exists in the tenant. Surfaces as HTTP 409.
/// </summary>
public sealed class ConflictException : DomainException
{
    public ConflictException(string message) : base(message)
    {
    }
}
