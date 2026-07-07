using System.Collections.Concurrent;

namespace MobileAppDeployment.Services.GitHub;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IRepoMergeJobStore"/>.
/// </summary>
public class RepoMergeJobStore : IRepoMergeJobStore
{
    private readonly ConcurrentDictionary<string, RepoMergeJobState> _jobs = new();

    /// <inheritdoc />
    public RepoMergeJobState Create(string clientRepoName, string appName, string? repositoryUrl, int maxRetries)
    {
        RepoMergeJobState job = new()
        {
            JobId = Guid.NewGuid().ToString("N"),
            ClientRepoName = clientRepoName,
            AppName = appName,
            RepositoryUrl = repositoryUrl,
            MaxRetries = maxRetries,
            Status = RepoMergeJobStatus.Queued,
            PercentComplete = 0,
            CurrentMessage = "Queued — preparing to merge source code into the client repository.",
            Attempt = 0
        };

        _jobs[job.JobId] = job;
        return job;
    }

    /// <inheritdoc />
    public RepoMergeJobState? Get(string jobId) =>
        _jobs.TryGetValue(jobId, out RepoMergeJobState? job) ? job : null;

    /// <inheritdoc />
    public void UpdateProgress(string jobId, int percent, string message, RepoMergeJobStatus status, int attempt)
    {
        if (!_jobs.TryGetValue(jobId, out RepoMergeJobState? job))
        {
            return;
        }

        job.Status = status;
        job.PercentComplete = Math.Clamp(percent, 0, 100);
        job.CurrentMessage = message;
        job.Attempt = attempt;
    }

    /// <inheritdoc />
    public void MarkCompleted(string jobId, string message)
    {
        if (!_jobs.TryGetValue(jobId, out RepoMergeJobState? job))
        {
            return;
        }

        job.Status = RepoMergeJobStatus.Completed;
        job.PercentComplete = 100;
        job.CurrentMessage = message;
        job.CompletedAt = DateTimeOffset.UtcNow;
        job.ErrorMessage = null;
    }

    /// <inheritdoc />
    public void MarkFailed(string jobId, string errorMessage)
    {
        if (!_jobs.TryGetValue(jobId, out RepoMergeJobState? job))
        {
            return;
        }

        job.Status = RepoMergeJobStatus.Failed;
        job.CurrentMessage = "Repository merge failed.";
        job.ErrorMessage = errorMessage;
        job.CompletedAt = DateTimeOffset.UtcNow;
    }
}
