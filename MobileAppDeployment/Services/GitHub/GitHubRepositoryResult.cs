namespace MobileAppDeployment.Services.GitHub;

/// <summary>
/// Outcome of an attempt to create a GitHub client repository.
/// </summary>
public class GitHubRepositoryResult
{
    public bool Success { get; init; }

    public string? RepositoryName { get; init; }

    public string? HtmlUrl { get; init; }

    public string? CloneUrl { get; init; }

    public string? ErrorMessage { get; init; }

    public static GitHubRepositoryResult Skipped(string reason) => new()
    {
        Success = false,
        ErrorMessage = reason
    };

    public static GitHubRepositoryResult Succeeded(string repositoryName, string htmlUrl, string? cloneUrl) => new()
    {
        Success = true,
        RepositoryName = repositoryName,
        HtmlUrl = htmlUrl,
        CloneUrl = cloneUrl
    };

    public static GitHubRepositoryResult Failed(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}
