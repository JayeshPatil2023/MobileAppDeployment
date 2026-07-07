namespace MobileAppDeployment.Services.GitHub;

/// <summary>
/// Lifecycle status for a repository merge background job.
/// </summary>
public enum RepoMergeJobStatus
{
    Queued,
    Running,
    Retrying,
    Completed,
    Failed
}

/// <summary>
/// Live and final state for a repository merge job shown in the progress UI.
/// </summary>
public class RepoMergeJobState
{
    public string JobId { get; init; } = string.Empty;

    public string ClientRepoName { get; init; } = string.Empty;

    public string AppName { get; init; } = string.Empty;

    public string? RepositoryUrl { get; init; }

    public RepoMergeJobStatus Status { get; set; } = RepoMergeJobStatus.Queued;

    public int PercentComplete { get; set; }

    public string CurrentMessage { get; set; } = "Waiting to start...";

    public int Attempt { get; set; }

    public int MaxRetries { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }

    public bool IsTerminal => Status is RepoMergeJobStatus.Completed or RepoMergeJobStatus.Failed;
}
