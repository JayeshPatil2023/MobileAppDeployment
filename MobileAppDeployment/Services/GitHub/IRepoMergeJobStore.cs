namespace MobileAppDeployment.Services.GitHub;

/// <summary>
/// In-memory store for repository merge job progress (polled by the UI).
/// </summary>
public interface IRepoMergeJobStore
{
    /// <summary>
    /// Creates and registers a new merge job.
    /// </summary>
    RepoMergeJobState Create(string clientRepoName, string appName, string? repositoryUrl, int maxRetries);

    /// <summary>
    /// Gets the current job state, or null if the job id is unknown.
    /// </summary>
    RepoMergeJobState? Get(string jobId);

    /// <summary>
    /// Updates progress fields for a running job.
    /// </summary>
    void UpdateProgress(string jobId, int percent, string message, RepoMergeJobStatus status, int attempt);

    /// <summary>
    /// Marks a job as successfully completed.
    /// </summary>
    void MarkCompleted(string jobId, string message);

    /// <summary>
    /// Marks a job as failed after all retry attempts.
    /// </summary>
    void MarkFailed(string jobId, string errorMessage);
}
