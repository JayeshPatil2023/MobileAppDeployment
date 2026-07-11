namespace MobileAppDeployment.Options;

/// <summary>
/// SMTP credentials for outbound email via Mailgun.
/// Bound from the <c>MailgunSMTP</c> section in appsettings.
/// </summary>
/// <remarks>
/// Treat <see cref="SMTPPassword"/> as a secret. Prefer environment-specific
/// overrides (User Secrets / environment variables) over committing real values.
/// </remarks>
public class MailgunSmtpOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "MailgunSMTP";

    /// <summary>
    /// Mailgun SMTP host (for example <c>smtp.mailgun.org</c>).
    /// </summary>
    public string SMTPServer { get; set; } = string.Empty;

    /// <summary>
    /// SMTP port (typically 587 for STARTTLS or 465 for SSL).
    /// </summary>
    public int SMTPPort { get; set; }

    /// <summary>
    /// SMTP authentication username (often the full Mailgun SMTP login).
    /// </summary>
    public string SMTPUsername { get; set; } = string.Empty;

    /// <summary>
    /// SMTP authentication password.
    /// </summary>
    public string SMTPPassword { get; set; } = string.Empty;

    /// <summary>
    /// From address used on outbound messages (must be an authorized Mailgun sender).
    /// </summary>
    public string FromEmail { get; set; } = string.Empty;

    /// <summary>
    /// Optional display name shown next to the From address.
    /// </summary>
    public string FromDisplayName { get; set; } = "Systenics App Deployment";
}
