namespace MobileAppDeployment.Core.Interfaces.Infrastructure;

/// <summary>
/// Triggers GitHub Actions workflows using the workflow_dispatch API.
/// </summary>
public interface IGitHubWorkflowDispatchService
{
    /// <summary>
    /// Dispatches the configured workflow with client name, asset URLs, and build environment values.
    /// </summary>
    Task<GitHubWorkflowDispatchResult> TriggerAsync(
        string? clientName,
        string logoBlobUrl,
        string splashBlobUrl,
        string appBundleId,
        string appId,
        string projectId,
        CancellationToken cancellationToken = default);
}
