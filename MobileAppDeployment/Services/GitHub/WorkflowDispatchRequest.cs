namespace MobileAppDeployment.Services.GitHub;

/// <summary>
/// Input required to store assets and dispatch the base GitHub Actions workflow.
/// </summary>
public class WorkflowDispatchRequest
{
    /// <summary>
    /// Client repository / app name passed as workflow input <c>client_name</c>.
    /// </summary>
    public required string ClientName { get; init; }

    /// <summary>
    /// Logo PNG file (maps from Mobile App Icon on the Create form).
    /// </summary>
    public required IFormFile LogoFile { get; init; }

    /// <summary>
    /// Splash PNG file (maps from Launch Image on the Create form).
    /// </summary>
    public required IFormFile SplashFile { get; init; }

    /// <summary>
    /// App bundle ID (maps from iOS Bundle ID).
    /// </summary>
    public required string AppBundleId { get; init; }

    /// <summary>
    /// OneSignal App ID.
    /// </summary>
    public required string AppId { get; init; }

    /// <summary>
    /// OneSignal Project / Sender ID.
    /// </summary>
    public required string ProjectId { get; init; }

    /// <summary>
    /// Optional banner message after deployment save.
    /// </summary>
    public string? SavedMessage { get; init; }
}
