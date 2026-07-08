namespace MobileAppDeployment.Options;

/// <summary>
/// Configuration for the admin token-issuance API and client form URLs.
/// Bound from the <c>FormAccess</c> section in appsettings.
/// </summary>
public class FormAccessOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "FormAccess";

    /// <summary>
    /// Fixed admin API key required in the <c>X-Api-Key</c> header when issuing tokens.
    /// Treat as a secret; override per environment without committing real production values.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional public base URL used when building the shareable form link in API responses
    /// (for example an ngrok tunnel). When empty, the request's current host is used.
    /// </summary>
    public string? PublicBaseUrl { get; set; }
}
