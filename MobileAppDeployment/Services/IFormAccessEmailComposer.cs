using MobileAppDeployment.Models;

namespace MobileAppDeployment.Services;

/// <summary>
/// Builds and sends the client-facing form-access link email.
/// </summary>
public interface IFormAccessEmailComposer
{
    /// <summary>
    /// Sends the shareable form URL to the client when an email address is supplied.
    /// </summary>
    /// <param name="toEmail">Client email address.</param>
    /// <param name="tokenResponse">Issued token details including <see cref="FormAccessTokenResponse.FormUrl"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Send result; callers should not treat token issuance as failed when email fails.</returns>
    Task<EmailSendResult> SendFormLinkAsync(
        string toEmail,
        FormAccessTokenResponse tokenResponse,
        CancellationToken cancellationToken = default);
}
