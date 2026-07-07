namespace MobileAppDeployment.Services.GitHub;

/// <summary>
/// Triggers GitHub Actions workflows using the workflow_dispatch API.
/// </summary>
public interface IGitHubWorkflowDispatchService
{
    /// <summary>
    /// Dispatches the configured workflow with optional input overrides.
    /// </summary>
    Task<GitHubWorkflowDispatchResult> TriggerAsync(string? clientName = null, CancellationToken cancellationToken = default);
}
