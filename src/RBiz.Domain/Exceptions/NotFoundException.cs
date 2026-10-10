namespace RBiz.Domain.Exceptions;

/// <summary>
/// Thrown when a requested record does not exist. Surfaces as HTTP 404.
/// </summary>
public sealed class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message)
    {
    }

    /// <summary>
    /// Builds a consistent "X with id Y was not found." message.
    /// </summary>
    public NotFoundException(string entityName, object key)
        : base($"{entityName} with id '{key}' was not found.")
    {
    }
}
