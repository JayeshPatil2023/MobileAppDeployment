using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using MobileAppDeployment.Options;

namespace MobileAppDeployment.Web.Filters;

/// <summary>
/// Authorization filter that validates the <c>X-Api-Key</c> header against
/// <c>FormAccess:ApiKey</c> using a constant-time comparison.
/// </summary>
public class ApiKeyAuthorizationFilter : IAuthorizationFilter
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    private readonly FormAccessOptions _options;
    private readonly ILogger<ApiKeyAuthorizationFilter> _logger;

    /// <summary>
    /// Creates the API key authorization filter.
    /// </summary>
    public ApiKeyAuthorizationFilter(
        IOptions<FormAccessOptions> options,
        ILogger<ApiKeyAuthorizationFilter> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!IsValidApiKey(context.HttpContext.Request))
        {
            _logger.LogWarning("Rejected request: invalid or missing API key.");
            context.Result = new UnauthorizedObjectResult(
                new { error = "Invalid or missing API key. Send a valid X-Api-Key header." });
        }
    }

    /// <summary>
    /// Compares the request API key to the configured value using a constant-time check.
    /// </summary>
    private bool IsValidApiKey(HttpRequest request)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogError("FormAccess:ApiKey is not configured; token API is locked.");
            return false;
        }

        if (!request.Headers.TryGetValue(ApiKeyHeaderName, out var provided) ||
            string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        return CryptographicEquals(_options.ApiKey, provided.ToString());
    }

    /// <summary>
    /// Constant-time ordinal string comparison for API key validation.
    /// </summary>
    private static bool CryptographicEquals(string expected, string actual)
    {
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
        byte[] actualBytes = Encoding.UTF8.GetBytes(actual);

        if (expectedBytes.Length != actualBytes.Length)
        {
            CryptographicOperations.FixedTimeEquals(expectedBytes, expectedBytes);
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
