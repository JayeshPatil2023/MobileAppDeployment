using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using MobileAppDeployment.Extensions;
using MobileAppDeployment.Core.Domain.Entities;
using MobileAppDeployment.Options;
using MobileAppDeployment.Core.Interfaces.Services;
using MobileAppDeployment.Web.Filters;

namespace MobileAppDeployment.Web.Controllers;

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
[EnableRateLimiting(RateLimitingExtensions.TokenApiPolicy)]
[ServiceFilter(typeof(ApiKeyAuthorizationFilter))]
public class FormAccessTokensController : ControllerBase
{
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
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        FormAccessTokenResponse response = await _tokenService.IssueAsync(
            request.ClientName,
            request.ClientAppName,
            BuildFormUrl);

        await TrySendFormLinkEmailAsync(request.Email, response, cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Sends the form link email when <paramref name="email"/> is non-empty; otherwise no-ops.
    /// </summary>
    private async Task TrySendFormLinkEmailAsync(
        string? email,
        FormAccessTokenResponse response,
        CancellationToken cancellationToken)
    {
        string? trimmedEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        if (trimmedEmail is null)
        {
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
    /// Builds the absolute client form URL for a token.
    /// </summary>
    private string BuildFormUrl(string token)
    {
        string path = Url.Action("Form", "AppDeployment", new { token }) ?? $"/AppDeployment/Form/{token}";

        if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
        {
            return $"{_options.PublicBaseUrl.TrimEnd('/')}{path}";
        }

        return $"{Request.Scheme}://{Request.Host}{path}";
    }
}
