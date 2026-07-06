using MobileAppDeployment.Models;

namespace MobileAppDeployment.Services.GitHub;

/// <summary>
/// Creates GitHub repositories for new client app deployments.
/// </summary>
public interface IGitHubRepositoryService
{
    /// <summary>
    /// Creates a GitHub repository named after the deployment's app name.
    /// </summary>
    /// <param name="deployment">Saved deployment containing the client app name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Structured result with repository URL or error details.</returns>
    Task<GitHubRepositoryResult> CreateClientRepositoryAsync(AppDeployment deployment, CancellationToken cancellationToken = default);
}
