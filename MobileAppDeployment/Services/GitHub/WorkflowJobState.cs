namespace MobileAppDeployment.Services.GitHub;

/// <summary>
/// Lifecycle status for a base workflow dispatch job shown in the progress UI.
/// </summary>
public enum WorkflowJobStatus
{
    Queued,
    Running,
    Retrying,
    Completed,
    Failed
}

/// <summary>
/// Live and final state for a workflow dispatch job.
/// </summary>
public class WorkflowJobState
{
    /// <summary>
    /// Unique job identifier used by the progress page polling endpoint.
    /// </summary>
    public string JobId { get; init; } = string.Empty;

    /// <summary>
    /// Client / app name used as the workflow client_name input.
    /// </summary>
    public string ClientName { get; init; } = string.Empty;

    /// <summary>
    /// Optional message shown after deployment save (before workflow finishes).
    /// </summary>
    public string? SavedMessage { get; set; }

    /// <summary>
    /// Current lifecycle status.
    /// </summary>
    public WorkflowJobStatus Status { get; set; } = WorkflowJobStatus.Queued;

    /// <summary>
    /// Progress percentage from 0 to 100.
    /// </summary>
    public int PercentComplete { get; set; }

    /// <summary>
    /// Human-readable status message for the UI.
    /// </summary>
    public string CurrentMessage { get; set; } = "Waiting to start...";

    /// <summary>
    /// Current attempt number (1-based).
    /// </summary>
    public int Attempt { get; set; }

    /// <summary>
    /// Configured maximum attempts (initial run + retries).
    /// </summary>
    public int MaxRetries { get; set; }

    /// <summary>
    /// Error details when the job fails.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// UTC time when the job was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// UTC time when the job reached a terminal state.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// True when the job no longer needs polling.
    /// </summary>
    public bool IsTerminal => Status is WorkflowJobStatus.Completed or WorkflowJobStatus.Failed;
}
