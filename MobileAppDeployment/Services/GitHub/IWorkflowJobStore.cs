namespace MobileAppDeployment.Services.GitHub;

/// <summary>
/// In-memory store for workflow dispatch job progress (polled by the UI).
/// </summary>
public interface IWorkflowJobStore
{
    /// <summary>
    /// Creates and registers a new workflow job.
    /// </summary>
    WorkflowJobState Create(string clientName, int maxRetries, string? savedMessage);

    /// <summary>
    /// Gets the current job state, or null if the job id is unknown.
    /// </summary>
    WorkflowJobState? Get(string jobId);

    /// <summary>
    /// Updates progress fields for a running job.
    /// </summary>
    void UpdateProgress(string jobId, int percent, string message, WorkflowJobStatus status, int attempt);

    /// <summary>
    /// Marks a job as successfully completed.
    /// </summary>
    void MarkCompleted(string jobId, string message);

    /// <summary>
    /// Marks a job as failed after all retry attempts.
    /// </summary>
    void MarkFailed(string jobId, string errorMessage);
}
