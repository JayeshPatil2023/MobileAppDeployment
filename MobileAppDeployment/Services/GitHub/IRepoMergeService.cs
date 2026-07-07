namespace MobileAppDeployment.Services.GitHub;

/// <summary>
/// Starts and monitors background repository merge jobs after client repo creation.
/// </summary>
public interface IRepoMergeService
{
    /// <summary>
    /// Queues a background merge job and returns its identifier for progress polling.
    /// </summary>
    string StartMergeJob(string clientRepoName, string appName, string? repositoryUrl);

    /// <summary>
    /// Gets the current merge job state for UI polling.
    /// </summary>
    RepoMergeJobState? GetJob(string jobId);
}
