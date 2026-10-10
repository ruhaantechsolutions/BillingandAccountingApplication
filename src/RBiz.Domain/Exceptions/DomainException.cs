namespace RBiz.Domain.Exceptions;

/// <summary>
/// Base class for every error that represents a broken business rule rather than a bug.
/// The API translates these into a 4xx response carrying <see cref="Exception.Message"/>, so the
/// message must always be safe and useful to show to an end user.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }

    protected DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
