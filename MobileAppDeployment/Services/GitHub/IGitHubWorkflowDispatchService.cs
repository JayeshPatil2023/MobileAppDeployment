namespace MobileAppDeployment.Services.GitHub;

/// <summary>
/// Triggers GitHub Actions workflows using the workflow_dispatch API.
/// </summary>
public interface IGitHubWorkflowDispatchService
{
    /// <summary>
    /// Dispatches the configured workflow.
    /// </summary>
    Task<GitHubWorkflowDispatchResult> TriggerAsync(CancellationToken cancellationToken = default);
}
