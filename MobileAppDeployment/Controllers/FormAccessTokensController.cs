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
/// </remarks>
[ApiController]
[Route("api/form-access-tokens")]
public class FormAccessTokensController : ControllerBase
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    private readonly IFormAccessTokenService _tokenService;
    private readonly FormAccessOptions _options;
    private readonly ILogger<FormAccessTokensController> _logger;

    /// <summary>
    /// Creates the token-issuance API controller.
    /// </summary>
    public FormAccessTokensController(
        IFormAccessTokenService tokenService,
        IOptions<FormAccessOptions> options,
        ILogger<FormAccessTokensController> logger)
    {
        _tokenService = tokenService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Issues (or returns an existing) form-access token for a client.
    /// </summary>
    /// <param name="request">Client name and client app name.</param>
    /// <returns>
    /// 200 with token + form URL; 401 when the API key is missing/invalid;
    /// 400 when the body fails validation.
    /// </returns>
    [HttpPost]
    [ProducesResponseType(typeof(FormAccessTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FormAccessTokenResponse>> Create([FromBody] CreateFormAccessTokenRequest request)
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

        return Ok(response);
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
