namespace MobileAppDeployment.Core.Interfaces.Repositories;

/// <summary>
/// Persistence operations for <see cref="AppDeployment"/> records.
/// </summary>
/// <remarks>
/// Methods stage EF changes only. Call <see cref="IUnitOfWork.SaveChangesAsync"/> to commit.
/// </remarks>
public interface IAppDeploymentRepository
{
    /// <summary>
    /// Returns all deployments ordered by creation date (newest first).
    /// </summary>
    Task<IEnumerable<AppDeployment>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single deployment by its primary key, or <c>null</c> if not found.
    /// </summary>
    Task<AppDeployment?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages a new deployment row. Persist with <see cref="IUnitOfWork.SaveChangesAsync"/> to obtain the Id.
    /// </summary>
    Task InsertAsync(AppDeployment appDeployment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages an update. Returns <c>false</c> when the row does not exist.
    /// </summary>
    Task<bool> UpdateAsync(AppDeployment appDeployment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages a delete. Returns <c>false</c> when the row does not exist.
    /// </summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
