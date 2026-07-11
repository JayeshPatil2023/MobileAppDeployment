using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using MobileAppDeployment.Options;

namespace MobileAppDeployment.Services;

/// <summary>
/// Sends email through Mailgun SMTP using <see cref="SmtpClient"/>.
/// </summary>
/// <remarks>
/// Configuration is read from <see cref="MailgunSmtpOptions"/>.
/// Callers should only invoke this when a recipient email was explicitly provided.
/// </remarks>
public class EmailService : IEmailService
{
    private readonly MailgunSmtpOptions _options;
    private readonly ILogger<EmailService> _logger;

    /// <summary>
    /// Creates the Mailgun SMTP email service.
    /// </summary>
    public EmailService(IOptions<MailgunSmtpOptions> options, ILogger<EmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<EmailSendResult> SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? plainTextBody = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured())
        {
            _logger.LogError("MailgunSMTP settings are incomplete; cannot send email to {ToEmail}.", toEmail);
            return EmailSendResult.Failure("Email service is not configured. Check MailgunSMTP settings.");
        }

        if (string.IsNullOrWhiteSpace(toEmail))
        {
            return EmailSendResult.Failure("Recipient email is required.");
        }

        try
        {
            using var message = BuildMessage(toEmail.Trim(), subject, htmlBody, plainTextBody);
            using var client = CreateSmtpClient();

            // SmtpClient.SendMailAsync does not accept CancellationToken on older overloads;
            // honor cancellation before starting the network call.
            cancellationToken.ThrowIfCancellationRequested();

            await client.SendMailAsync(message, cancellationToken);

            _logger.LogInformation("Email sent to {ToEmail} with subject '{Subject}'.", toEmail, subject);
            return EmailSendResult.Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Do not leak SMTP credentials or raw server banners to API consumers.
            _logger.LogError(ex, "Failed to send email to {ToEmail}.", toEmail);
            return EmailSendResult.Failure("Failed to send email. Check SMTP configuration and try again.");
        }
    }

    /// <summary>
    /// Returns true when all required Mailgun SMTP settings are present.
    /// </summary>
    private bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(_options.SMTPServer)
            && _options.SMTPPort > 0
            && !string.IsNullOrWhiteSpace(_options.SMTPUsername)
            && !string.IsNullOrWhiteSpace(_options.SMTPPassword)
            && !string.IsNullOrWhiteSpace(_options.FromEmail);
    }

    /// <summary>
    /// Builds a multipart alternate MIME message (plain text + HTML) when both bodies are supplied.
    /// </summary>
    private MailMessage BuildMessage(string toEmail, string subject, string htmlBody, string? plainTextBody)
    {
        var from = new MailAddress(
            _options.FromEmail.Trim(),
            string.IsNullOrWhiteSpace(_options.FromDisplayName)
                ? null
                : _options.FromDisplayName.Trim());

        var message = new MailMessage
        {
            From = from,
            Subject = subject,
            BodyEncoding = System.Text.Encoding.UTF8,
            SubjectEncoding = System.Text.Encoding.UTF8
        };

        message.To.Add(new MailAddress(toEmail));

        // Prefer multipart/alternative when plain text is available; otherwise send HTML only.
        if (!string.IsNullOrWhiteSpace(plainTextBody))
        {
            message.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(
                    plainTextBody,
                    System.Text.Encoding.UTF8,
                    System.Net.Mime.MediaTypeNames.Text.Plain));
            message.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(
                    htmlBody,
                    System.Text.Encoding.UTF8,
                    System.Net.Mime.MediaTypeNames.Text.Html));
        }
        else
        {
            message.IsBodyHtml = true;
            message.Body = htmlBody;
        }

        return message;
    }

    /// <summary>
    /// Creates an SMTP client pointed at the configured Mailgun relay.
    /// </summary>
    private SmtpClient CreateSmtpClient()
    {
        // Port 587 uses STARTTLS; EnableSsl must be true for Mailgun.
        return new SmtpClient(_options.SMTPServer, _options.SMTPPort)
        {
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_options.SMTPUsername, _options.SMTPPassword)
        };
    }
}
