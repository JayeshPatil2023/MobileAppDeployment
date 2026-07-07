namespace MobileAppDeployment.Services.GitHub;

/// <summary>
/// Configuration used to trigger a GitHub Actions workflow via workflow_dispatch.
/// </summary>
public class GitHubWorkflowDispatchOptions
{
    /// <summary>
    /// Enables or disables workflow dispatch from the application UI.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Owner (user or organization) containing the target repository.
    /// </summary>
    public string Owner { get; set; } = "systenics";

    /// <summary>
    /// Repository name containing the workflow definition.
    /// </summary>
    public string Repository { get; set; } = "SA_BaseMVCProject";

    /// <summary>
    /// Workflow file name or workflow id (for example: main.yml).
    /// </summary>
    public string Workflow { get; set; } = "main.yml";

    /// <summary>
    /// Branch or tag ref to run the workflow on.
    /// </summary>
    public string Ref { get; set; } = "master";
}
