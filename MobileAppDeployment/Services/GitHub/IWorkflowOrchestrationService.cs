namespace MobileAppDeployment.Services.GitHub;

/// <summary>
/// Orchestrates asset upload, workflow dispatch with retries, and live job progress.
/// Shared by the production Create form and the Index R&amp;D trigger form.
/// </summary>
public interface IWorkflowOrchestrationService
{
    /// <summary>
    /// Starts a background workflow job and returns its id for progress polling.
    /// </summary>
    /// <remarks>
    /// Asset files are copied into memory before the HTTP request ends so the
    /// background worker can upload them safely.
    /// </remarks>
    Task<string> StartWorkflowJobAsync(WorkflowDispatchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current workflow job state for UI polling.
    /// </summary>
    WorkflowJobState? GetJob(string jobId);
}
