using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using MobileAppDeployment.Core.Domain.Entities;
using MobileAppDeployment.Options;
using MobileAppDeployment.Core.Interfaces.Services;

namespace MobileAppDeployment.Infrastructure.Email;

/// <summary>
/// Sends transactional email via Mailgun SMTP using <strong>MailKit 4.16+</strong>.
/// </summary>
/// <remarks>
/// <para>
/// This implementation replaces the deprecated <c>System.Net.Mail.SmtpClient</c> class.
/// <c>System.Net.Mail.SmtpClient</c> is not suitable for production use because:
/// <list type="bullet">
///   <item>It does not support modern async I/O — the async methods are fake (thread-pool blocking).</item>
///   <item>It creates one TCP connection per send rather than pooling.</item>
///   <item>It is marked [Obsolete] in .NET and may be removed in a future version.</item>
/// </list>
/// </para>
/// <para>
/// <strong>Connection strategy:</strong> MailKit opens a new SMTP connection per send.
/// For this application's low email volume (one per token issuance), connection-per-send
/// is acceptable. If email volume grows significantly, implement an object pool or
/// move to the Mailgun HTTP API which supports high throughput natively.
/// </para>
/// <para>
/// <strong>TLS:</strong> STARTTLS is used on port 587 (SMTP submission). The
/// <c>SecureSocketOptions.StartTls</c> option requires TLS and fails if the server
/// does not offer it. Do not use <c>StartTlsWhenAvailable</c> — that silently
/// falls back to plaintext if TLS negotiation fails.
/// </para>
/// </remarks>
public class MailKitEmailService : IEmailService
{
    private readonly MailgunSmtpOptions _options;
    private readonly ILogger<MailKitEmailService> _logger;

    /// <summary>Creates the MailKit email service.</summary>
    public MailKitEmailService(
        IOptions<MailgunSmtpOptions> options,
        ILogger<MailKitEmailService> logger)
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
        // ── Validate configuration ─────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(_options.SMTPServer) ||
            string.IsNullOrWhiteSpace(_options.SMTPUsername) ||
            string.IsNullOrWhiteSpace(_options.SMTPPassword) ||
            string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            string configError = "SMTP configuration is incomplete. Email not sent. " +
                                 "Provide MailgunSMTP:SMTPServer, SMTPUsername, SMTPPassword, and FromEmail via user-secrets.";
            _logger.LogWarning(configError);
            return EmailSendResult.Failure(configError);
        }

        // ── Build MimeMessage ─────────────────────────────────────────────
        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(_options.FromDisplayName, _options.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        // Multipart/alternative: email clients use the first format they understand.
        // Put plain text first so legacy/text-only clients get a readable fallback.
        var body = new MultipartAlternative
        {
            new TextPart(TextFormat.Plain) { Text = plainTextBody ?? string.Empty },
            new TextPart(TextFormat.Html)  { Text = htmlBody                       }
        };

        message.Body = body;

        // ── Send via SMTP ──────────────────────────────────────────────────
        try
        {
            using SmtpClient client = new();

            // RequireStartTls: fail hard if TLS is not available rather than
            // silently sending credentials over plain text.
            await client.ConnectAsync(
                _options.SMTPServer,
                _options.SMTPPort,
                SecureSocketOptions.StartTls,
                cancellationToken);

            await client.AuthenticateAsync(
                _options.SMTPUsername,
                _options.SMTPPassword,
                cancellationToken);

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);

            _logger.LogInformation("Email sent successfully to {Recipient} with subject '{Subject}'.",
                toEmail, subject);

            return EmailSendResult.Success();
        }
        catch (OperationCanceledException)
        {
            // Propagate cancellation — caller decides whether to log or ignore.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send email to {Recipient} with subject '{Subject}'.",
                toEmail, subject);

            return EmailSendResult.Failure(ex.Message);
        }
    }
}
