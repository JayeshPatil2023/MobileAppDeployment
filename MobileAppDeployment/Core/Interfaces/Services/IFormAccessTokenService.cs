using MobileAppDeployment.Core.Domain.Entities;

namespace MobileAppDeployment.Core.Interfaces.Services;

/// <summary>
/// Application service for issuing and resolving client form-access tokens.
/// </summary>
public interface IFormAccessTokenService
{
    /// <summary>
    /// Issues a new token for the client, or returns the existing active token for the same names.
    /// </summary>
    /// <param name="clientName">Client / organization name.</param>
    /// <param name="clientAppName">Client application name.</param>
    /// <param name="formUrlBuilder">Builds the absolute form URL for a given token string.</param>
    Task<FormAccessTokenResponse> IssueAsync(
        string clientName,
        string clientAppName,
        Func<string, string> formUrlBuilder);

    /// <summary>
    /// Loads an active token by its opaque value, or null when missing/inactive.
    /// </summary>
    Task<FormAccessToken?> ResolveActiveAsync(string token);

    /// <summary>
    /// Links the token to the deployment created on first successful submit.
    /// </summary>
    /// <remarks>
    /// Idempotent for already-linked tokens: if the token already points at a deployment,
    /// this is a no-op (edit submits must not overwrite the link).
    /// </remarks>
    Task MarkSubmittedAsync(string token, int appDeploymentId);
}
