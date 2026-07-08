using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MobileAppDeployment.Services.GitHub;

namespace MobileAppDeployment.Services;

/// <summary>
/// Persists workflow trigger assets to wwwroot and builds absolute URLs for GitHub Actions.
/// </summary>
public class WorkflowAssetStorageService : IWorkflowAssetStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly GitHubWorkflowDispatchOptions _options;

    public WorkflowAssetStorageService(
        IWebHostEnvironment environment,
        IHttpContextAccessor httpContextAccessor,
        IOptions<GitHubWorkflowDispatchOptions> options)
    {
        _environment = environment;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<string> SaveAndGetPublicUrlAsync(
        IFormFile file,
        string clientName,
        string assetKey,
        IEnumerable<string> allowedExtensions)
    {
        if (file.Length == 0)
        {
            throw new InvalidOperationException($"{assetKey} file is required.");
        }

        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        HashSet<string> allowed = allowedExtensions.Select(x => x.ToLowerInvariant()).ToHashSet();
        if (!allowed.Contains(extension))
        {
            throw new InvalidOperationException(
                $"Invalid file type for {assetKey}. Allowed: {string.Join(", ", allowed)}.");
        }

        string safeClientName = SanitizeFolderName(clientName);
        string uploadDirectory = Path.Combine(_environment.WebRootPath, "uploads", "workflow-assets", safeClientName);
        Directory.CreateDirectory(uploadDirectory);

        string fileName = $"{assetKey}{extension}";
        string physicalPath = Path.Combine(uploadDirectory, fileName);

        await using FileStream stream = new(physicalPath, FileMode.Create);
        await file.CopyToAsync(stream);

        string relativePath = $"/uploads/workflow-assets/{safeClientName}/{fileName}";
        return $"{ResolvePublicBaseUrl()}{relativePath}";
    }

    /// <summary>
    /// Uses configured <see cref="GitHubWorkflowDispatchOptions.PublicBaseUrl"/> when set.
    /// Does not fall back to localhost/request host for GitHub Actions downloads — runners cannot reach it.
    /// </summary>
    private string ResolvePublicBaseUrl()
    {
        // Treat empty/whitespace as unset (Development JSON often sets "" and would override appsettings.json).
        string configured = _options.PublicBaseUrl?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            string baseUrl = configured.TrimEnd('/');
            if (IsLocalOrLoopbackUrl(baseUrl))
            {
                throw new InvalidOperationException(
                    "GitHub:WorkflowDispatch:PublicBaseUrl must be a publicly reachable URL (for example an ngrok HTTPS URL). " +
                    "localhost / 127.0.0.1 values cannot be downloaded by GitHub Actions runners.");
            }

            return baseUrl;
        }

        HttpRequest? request = _httpContextAccessor.HttpContext?.Request;
        string? requestBase = request is null ? null : $"{request.Scheme}://{request.Host}".TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(requestBase) && !IsLocalOrLoopbackUrl(requestBase))
        {
            return requestBase;
        }

        throw new InvalidOperationException(
            "GitHub:WorkflowDispatch:PublicBaseUrl is not configured. " +
            "Set it to your ngrok or production public HTTPS base URL so GitHub Actions can download logo/splash assets. " +
            "Example: https://your-subdomain.ngrok-free.dev");
    }

    /// <summary>
    /// True when the URL would not be reachable from GitHub-hosted runners.
    /// </summary>
    private static bool IsLocalOrLoopbackUrl(string absoluteUrl)
    {
        if (!Uri.TryCreate(absoluteUrl, UriKind.Absolute, out Uri? uri))
        {
            return true;
        }

        string host = uri.Host;
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || host is "127.0.0.1" or "::1"
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Converts client name into a safe directory segment.
    /// </summary>
    private static string SanitizeFolderName(string clientName)
    {
        string trimmed = clientName.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("Client name is required to store workflow assets.");
        }

        string sanitized = Regex.Replace(trimmed, @"[^a-zA-Z0-9\-_]", "-");
        sanitized = Regex.Replace(sanitized, @"-+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "client" : sanitized;
    }
}
