namespace MobileAppDeployment.Core.Interfaces.Services;

/// <summary>
/// Orchestrates asset upload, workflow dispatch with retries, and live job progress.
/// </summary>
public interface IWorkflowOrchestrationService
{
    /// <summary>
    /// Starts a background workflow job from uploaded form files and returns its id for progress polling.
    /// </summary>
    /// <remarks>
    /// Asset files are read on the request thread while <see cref="IFormFile"/> streams are still valid.
    /// Only the GitHub API dispatch is retried in the background.
    /// </remarks>
    Task<string> StartWorkflowJobAsync(WorkflowDispatchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a background workflow job from a saved <see cref="AppDeployment"/> record.
    /// </summary>
    /// <remarks>
    /// Used when the user explicitly clicks <strong>Start App Deployment Process</strong> after saving.
    /// Logo and splash are copied from the deployment's persisted asset paths into workflow-assets.
    /// </remarks>
    /// <param name="deployment">Saved deployment row including asset paths and workflow input fields.</param>
    /// <param name="savedMessage">Optional banner shown on the workflow progress page.</param>
    /// <param name="cancellationToken">Request cancellation token (asset copy only; dispatch continues in background).</param>
    Task<string> StartWorkflowJobFromDeploymentAsync(
        AppDeployment deployment,
        string? savedMessage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current workflow job state for UI polling.
    /// </summary>
    WorkflowJobState? GetJob(string jobId);
}
