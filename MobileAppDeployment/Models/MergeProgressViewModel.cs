namespace MobileAppDeployment.Models;

/// <summary>
/// View model for the live repository merge progress page shown after client repo creation.
/// </summary>
public class MergeProgressViewModel
{
    public string JobId { get; init; } = string.Empty;

    public string AppName { get; init; } = string.Empty;

    public string ClientRepoName { get; init; } = string.Empty;

    public string? RepositoryUrl { get; init; }

    /// <summary>
    /// Message shown after deployment save and GitHub repo creation (before merge completes).
    /// </summary>
    public string? RepoCreatedMessage { get; init; }
}
