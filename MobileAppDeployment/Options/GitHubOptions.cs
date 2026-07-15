namespace MobileAppDeployment.Options;

/// <summary>
/// Configuration for GitHub repository automation.
/// </summary>
public class GitHubOptions
{
    public const string SectionName = "GitHub";

    /// <summary>
    /// GitHub Personal Access Token with <c>repo</c> scope.
    /// Prefer User Secrets or environment variable GITHUB_TOKEN in production.
    /// </summary>
    public string PersonalAccessToken { get; set; } = string.Empty;

    /// <summary>
    /// GitHub account that owns created repositories.
    /// </summary>
    public string Owner { get; set; } = "JayeshPatil2023";

    /// <summary>
    /// REST API endpoint used to create repositories for the authenticated user.
    /// </summary>
    public string CreateRepositoryEndpoint { get; set; } = "https://api.github.com/user/repos";

    /// <summary>
    /// When false, repository creation is skipped (useful for local development).
    /// </summary>
    public bool Enabled { get; set; } = true;
}
