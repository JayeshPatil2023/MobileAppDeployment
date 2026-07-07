namespace MobileAppDeployment.Services.GitHub;

/// <summary>
/// Configuration for merging source code into a newly created client repository.
/// </summary>
public class RepoMergeOptions
{
    /// <summary>
    /// When false, repository merge is skipped after client repo creation.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// PowerShell script file name under the Scripts folder.
    /// </summary>
    public string ScriptFileName { get; set; } = "Merge-LatestClientChanges.ps1";

    /// <summary>
    /// Maximum number of execution attempts (initial run + retries).
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Delay in seconds between retry attempts after a failure.
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 5;

    /// <summary>
    /// GitHub organization/user that owns the source repository.
    /// </summary>
    public string SourceOwner { get; set; } = "systenics";

    /// <summary>
    /// Source repository name to merge from.
    /// </summary>
    public string SourceRepository { get; set; } = "SA_AWDemoMobile";

    /// <summary>
    /// Source branch to merge from.
    /// </summary>
    public string SourceBranch { get; set; } = "master_client";

    /// <summary>
    /// Target branch created/updated on the client repository.
    /// </summary>
    public string ClientBranch { get; set; } = "master_dev";

    /// <summary>
    /// Local working directory root for git operations.
    /// </summary>
    public string WorkingDirectoryRoot { get; set; } = @"C:\Application";
}
