namespace MobileAppDeployment.Services.GitHub;

/// <summary>
/// Result for a GitHub workflow dispatch attempt.
/// </summary>
public class GitHubWorkflowDispatchResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public static GitHubWorkflowDispatchResult Succeeded(string message) => new()
    {
        Success = true,
        Message = message
    };

    public static GitHubWorkflowDispatchResult Failed(string message) => new()
    {
        Success = false,
        Message = message
    };
}
