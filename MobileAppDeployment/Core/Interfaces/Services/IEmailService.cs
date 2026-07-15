namespace MobileAppDeployment.Core.Interfaces.Services;

/// <summary>
/// Outbound email abstraction used by application features (form-access links, etc.).
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an HTML (with plain-text fallback) email through the configured Mailgun SMTP relay.
    /// </summary>
    /// <param name="toEmail">Recipient address.</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="htmlBody">HTML body content.</param>
    /// <param name="plainTextBody">Optional plain-text alternative for clients that prefer it.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result describing success or failure without throwing for SMTP/transport errors.</returns>
    Task<EmailSendResult> SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? plainTextBody = null,
        CancellationToken cancellationToken = default);
}
