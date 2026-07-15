namespace MobileAppDeployment.Core.Interfaces.Repositories;

/// <summary>
/// Persistence operations for <see cref="FormAccessToken"/> records.
/// </summary>
/// <remarks>
/// Methods stage EF changes only. Call <see cref="IUnitOfWork.SaveChangesAsync"/> to commit.
/// </remarks>
public interface IFormAccessTokenRepository
{
    /// <summary>
    /// Looks up a token by its opaque string value.
    /// </summary>
    Task<FormAccessToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an active token for the same client name + app name pair (case-insensitive).
    /// </summary>
    Task<FormAccessToken?> FindActiveByClientAsync(string clientName, string clientAppName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages a new token row. Persist with <see cref="IUnitOfWork.SaveChangesAsync"/>.
    /// </summary>
    Task InsertAsync(FormAccessToken entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages an update for a tracked or attached entity.
    /// </summary>
    Task UpdateAsync(FormAccessToken entity, CancellationToken cancellationToken = default);
}
