using System.Net;
using System.Text;
using MobileAppDeployment.Models;

namespace MobileAppDeployment.Services;

/// <summary>
/// Composes the HTML/plain-text email that delivers a client's form-access magic link.
/// </summary>
public class FormAccessEmailComposer : IFormAccessEmailComposer
{
    private readonly IEmailService _emailService;

    /// <summary>
    /// Creates the form-access email composer.
    /// </summary>
    public FormAccessEmailComposer(IEmailService emailService)
    {
        _emailService = emailService;
    }

    /// <inheritdoc />
    public Task<EmailSendResult> SendFormLinkAsync(
        string toEmail,
        FormAccessTokenResponse tokenResponse,
        CancellationToken cancellationToken = default)
    {
        string subject = $"Your app deployment form link — {tokenResponse.ClientAppName}";
        string htmlBody = BuildHtmlBody(tokenResponse);
        string plainTextBody = BuildPlainTextBody(tokenResponse);

        return _emailService.SendAsync(toEmail, subject, htmlBody, plainTextBody, cancellationToken);
    }

    /// <summary>
    /// Builds a simple, branded HTML email body containing the form URL.
    /// </summary>
    private static string BuildHtmlBody(FormAccessTokenResponse tokenResponse)
    {
        // Encode user-supplied names and the URL so a malicious client name cannot inject HTML.
        string clientName = WebUtility.HtmlEncode(tokenResponse.ClientName);
        string appName = WebUtility.HtmlEncode(tokenResponse.ClientAppName);
        string formUrl = WebUtility.HtmlEncode(tokenResponse.FormUrl);
        string modeHint = tokenResponse.IsSubmitted
            ? "This link opens your existing deployment form so you can review or update details."
            : "This link opens a secure form where you can submit your mobile app deployment details.";

        var sb = new StringBuilder();
        sb.Append("""
            <!DOCTYPE html>
            <html>
            <head><meta charset="utf-8" /></head>
            <body style="font-family:Segoe UI,Arial,sans-serif;color:#1f2937;line-height:1.5;margin:0;padding:24px;background:#f8fafc;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:560px;margin:0 auto;background:#ffffff;border:1px solid #e5e7eb;border-radius:8px;">
                <tr>
                  <td style="padding:28px 28px 8px 28px;">
                    <p style="margin:0 0 8px 0;font-size:12px;letter-spacing:.04em;text-transform:uppercase;color:#64748b;">Systenics App Deployment</p>
                    <h1 style="margin:0 0 16px 0;font-size:22px;color:#0f172a;">Your secure form link is ready</h1>
                    <p style="margin:0 0 12px 0;">Hello,</p>
                    <p style="margin:0 0 12px 0;">
                      A form access link has been issued for <strong>
            """);
        sb.Append(clientName);
        sb.Append(" — ");
        sb.Append(appName);
        sb.Append("""
            </strong>.
                    </p>
                    <p style="margin:0 0 20px 0;">
            """);
        sb.Append(modeHint);
        sb.Append("""
                    </p>
                    <p style="margin:0 0 24px 0;">
                      <a href="
            """);
        sb.Append(formUrl);
        sb.Append("""
            " style="display:inline-block;background:#2563eb;color:#ffffff;text-decoration:none;padding:12px 18px;border-radius:6px;font-weight:600;">
                        Open app deployment form
                      </a>
                    </p>
                    <p style="margin:0 0 8px 0;font-size:13px;color:#64748b;">Or copy and paste this URL into your browser:</p>
                    <p style="margin:0 0 20px 0;font-size:13px;word-break:break-all;color:#334155;">
            """);
        sb.Append(formUrl);
        sb.Append("""
                    </p>
                    <p style="margin:0;font-size:12px;color:#94a3b8;">
                      If you were not expecting this email, you can ignore it. Do not forward this link — it grants access to your submission form.
                    </p>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """);

        return sb.ToString();
    }

    /// <summary>
    /// Builds a plain-text fallback body for email clients that do not render HTML.
    /// </summary>
    private static string BuildPlainTextBody(FormAccessTokenResponse tokenResponse)
    {
        string modeHint = tokenResponse.IsSubmitted
            ? "This link opens your existing deployment form so you can review or update details."
            : "This link opens a secure form where you can submit your mobile app deployment details.";

        return
            $"""
            Systenics App Deployment

            Your secure form link is ready for {tokenResponse.ClientName} — {tokenResponse.ClientAppName}.

            {modeHint}

            Open the form:
            {tokenResponse.FormUrl}

            If you were not expecting this email, you can ignore it. Do not forward this link.
            """;
    }
}
