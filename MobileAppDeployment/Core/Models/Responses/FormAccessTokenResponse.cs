namespace MobileAppDeployment.Core.Models.Responses;

/// <summary>
/// Response returned after issuing (or resolving) a client form-access token.
/// </summary>
public class FormAccessTokenResponse
{
    /// <summary>
    /// Opaque token value to embed in the form URL.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Client / organization name associated with the token.
    /// </summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// Client application name associated with the token.
    /// </summary>
    public string ClientAppName { get; set; } = string.Empty;

    /// <summary>
    /// Absolute URL the admin should share with the client.
    /// </summary>
    public string FormUrl { get; set; } = string.Empty;

    /// <summary>
    /// True when an existing active token for the same client/app was returned instead of creating a new one.
    /// </summary>
    public bool AlreadyExisted { get; set; }

    /// <summary>
    /// True when the client has already submitted the form at least once (edit mode).
    /// </summary>
    public bool IsSubmitted { get; set; }

    /// <summary>
    /// True when an email was requested and the SMTP send succeeded.
    /// </summary>
    public bool EmailSent { get; set; }

    /// <summary>
    /// Recipient used for the optional email send; null when email was not requested.
    /// </summary>
    public string? EmailRecipient { get; set; }

    /// <summary>
    /// Safe error detail when email was requested but sending failed; otherwise null.
    /// </summary>
    /// <remarks>
    /// Token issuance still succeeds when email fails — use <see cref="FormUrl"/> to share manually.
    /// </remarks>
    public string? EmailError { get; set; }
}
