namespace RBiz.Application.Interfaces;

/// <summary>
/// Commits the changes tracked by the repositories in one transaction. Keeping the save
/// separate from the repositories lets a service make several changes and persist them
/// together with a single round trip to the database.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
