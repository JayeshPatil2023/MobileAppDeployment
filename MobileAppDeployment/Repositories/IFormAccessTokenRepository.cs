using MobileAppDeployment.Models;

namespace MobileAppDeployment.Repositories;

/// <summary>
/// Persistence operations for <see cref="FormAccessToken"/> records.
/// </summary>
public interface IFormAccessTokenRepository
{
    /// <summary>
    /// Looks up a token by its opaque string value.
    /// </summary>
    Task<FormAccessToken?> GetByTokenAsync(string token);

    /// <summary>
    /// Finds an active token for the same client name + app name pair (case-insensitive).
    /// </summary>
    /// <remarks>
    /// Used so re-issuing for the same client returns the existing shareable link
    /// instead of creating duplicate tokens.
    /// </remarks>
    Task<FormAccessToken?> FindActiveByClientAsync(string clientName, string clientAppName);

    /// <summary>
    /// Inserts a new token row and returns the saved entity (with generated Id).
    /// </summary>
    Task<FormAccessToken> InsertAsync(FormAccessToken entity);

    /// <summary>
    /// Persists changes already tracked on <paramref name="entity"/>.
    /// </summary>
    Task UpdateAsync(FormAccessToken entity);
}
