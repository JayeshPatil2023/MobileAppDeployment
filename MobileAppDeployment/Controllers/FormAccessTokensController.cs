using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MobileAppDeployment.Models;
using MobileAppDeployment.Options;
using MobileAppDeployment.Services;

namespace MobileAppDeployment.Controllers;

/// <summary>
/// Admin-only API for issuing client form-access tokens.
/// </summary>
/// <remarks>
/// Protected by a fixed API key from configuration (<c>FormAccess:ApiKey</c>),
/// sent in the <c>X-Api-Key</c> header. Intended for Postman / admin tools —
/// not for end clients.
/// <para>
/// When the optional <c>email</c> field is supplied, the generated form URL is
/// emailed to the client via Mailgun SMTP. Token issuance does not depend on email success.
/// </para>
/// </remarks>
[ApiController]
[Route("api/form-access-tokens")]
public class FormAccessTokensController : ControllerBase
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    private readonly IFormAccessTokenService _tokenService;
    private readonly IFormAccessEmailComposer _formAccessEmailComposer;
    private readonly FormAccessOptions _options;
    private readonly ILogger<FormAccessTokensController> _logger;

    /// <summary>
    /// Creates the token-issuance API controller.
    /// </summary>
    public FormAccessTokensController(
        IFormAccessTokenService tokenService,
        IFormAccessEmailComposer formAccessEmailComposer,
        IOptions<FormAccessOptions> options,
        ILogger<FormAccessTokensController> logger)
    {
        _tokenService = tokenService;
        _formAccessEmailComposer = formAccessEmailComposer;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Issues (or returns an existing) form-access token for a client.
    /// </summary>
    /// <param name="request">Client name, client app name, and optional email.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// 200 with token + form URL (and email status when requested);
    /// 401 when the API key is missing/invalid;
    /// 400 when the body fails validation.
    /// </returns>
    [HttpPost]
    [ProducesResponseType(typeof(FormAccessTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FormAccessTokenResponse>> Create([FromBody] CreateFormAccessTokenRequest request, CancellationToken cancellationToken)
    {
        if (!IsValidApiKey())
        {
            _logger.LogWarning("Rejected form-access token request: invalid or missing API key.");
            return Unauthorized(new { error = "Invalid or missing API key. Send a valid X-Api-Key header." });
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        FormAccessTokenResponse response = await _tokenService.IssueAsync(
            request.ClientName,
            request.ClientAppName,
            BuildFormUrl);

        // Business rule: email is optional. Only start the send pipeline when an address was provided.
        await TrySendFormLinkEmailAsync(request.Email, response, cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Sends the form link email when <paramref name="email"/> is non-empty; otherwise no-ops.
    /// </summary>
    /// <remarks>
    /// Email failure is recorded on the response but does not fail token issuance —
    /// the admin still receives <see cref="FormAccessTokenResponse.FormUrl"/> to share manually.
    /// </remarks>
    private async Task TrySendFormLinkEmailAsync(
        string? email,
        FormAccessTokenResponse response,
        CancellationToken cancellationToken)
    {
        string? trimmedEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        if (trimmedEmail is null)
        {
            // Explicit no-op path: token-only issuance without notifying the client by email.
            return;
        }

        response.EmailRecipient = trimmedEmail;

        EmailSendResult sendResult = await _formAccessEmailComposer.SendFormLinkAsync(
            trimmedEmail,
            response,
            cancellationToken);

        response.EmailSent = sendResult.Succeeded;
        response.EmailError = sendResult.Succeeded ? null : sendResult.ErrorMessage;

        if (!sendResult.Succeeded)
        {
            _logger.LogWarning(
                "Form-access token issued for {ClientAppName}, but email to {Email} failed: {Error}",
                response.ClientAppName,
                trimmedEmail,
                sendResult.ErrorMessage);
        }
    }

    /// <summary>
    /// Compares the request API key to the configured value using a constant-time check.
    /// </summary>
    private bool IsValidApiKey()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            // Misconfigured server must not accept any key — fail closed.
            _logger.LogError("FormAccess:ApiKey is not configured; token API is locked.");
            return false;
        }

        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var provided) ||
            string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        // Fixed-time compare reduces timing side-channels on the hardcoded admin key.
        return CryptographicEquals(_options.ApiKey, provided.ToString());
    }

    /// <summary>
    /// Builds the absolute client form URL for a token.
    /// </summary>
    private string BuildFormUrl(string token)
    {
        string path = Url.Action("Form", "AppDeployment", new { token }) ?? $"/AppDeployment/Form/{token}";

        if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
        {
            return $"{_options.PublicBaseUrl.TrimEnd('/')}{path}";
        }

        // Fall back to the host that received the admin API call (Postman against localhost, etc.).
        return $"{Request.Scheme}://{Request.Host}{path}";
    }

    /// <summary>
    /// Constant-time ordinal string comparison for API key validation.
    /// </summary>
    private static bool CryptographicEquals(string expected, string actual)
    {
        byte[] expectedBytes = System.Text.Encoding.UTF8.GetBytes(expected);
        byte[] actualBytes = System.Text.Encoding.UTF8.GetBytes(actual);

        // Length mismatch still runs a dummy FixedTimeEquals against expected to avoid short-circuit leaks.
        if (expectedBytes.Length != actualBytes.Length)
        {
            System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expectedBytes, expectedBytes);
            return false;
        }

        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
