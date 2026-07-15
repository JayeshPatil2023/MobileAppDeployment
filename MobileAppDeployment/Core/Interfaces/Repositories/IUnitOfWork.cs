namespace MobileAppDeployment.Core.Interfaces.Repositories;

/// <summary>
/// Coordinates atomic persistence across repositories that share one <c>DbContext</c>.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Persists all staged repository changes in a single database round-trip.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the save operation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
