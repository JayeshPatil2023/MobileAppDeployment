namespace MobileAppDeployment.Core.Models.ViewModels;

/// <summary>
/// View model for the live base workflow progress page.
/// </summary>
public class WorkflowProgressViewModel
{
    /// <summary>
    /// Job id polled by the progress UI.
    /// </summary>
    public string JobId { get; init; } = string.Empty;

    /// <summary>
    /// Client / app name shown in the progress header.
    /// </summary>
    public string ClientName { get; init; } = string.Empty;

    /// <summary>
    /// Optional message shown after deployment save (before workflow completes).
    /// </summary>
    public string? SavedMessage { get; init; }
}
