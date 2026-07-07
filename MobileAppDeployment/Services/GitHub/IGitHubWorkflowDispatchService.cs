namespace MobileAppDeployment.Services.GitHub;

/// <summary>
/// Triggers GitHub Actions workflows using the workflow_dispatch API.
/// </summary>
public interface IGitHubWorkflowDispatchService
{
    /// <summary>
    /// Dispatches the configured workflow with client name and asset URLs for Update-GitHubAssets.ps1.
    /// </summary>
    Task<GitHubWorkflowDispatchResult> TriggerAsync(
        string? clientName,
        string logoBlobUrl,
        string splashBlobUrl,
        CancellationToken cancellationToken = default);
}
