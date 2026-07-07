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
    /// Uses configured PublicBaseUrl when set; otherwise derives from the current HTTP request.
    /// </summary>
    private string ResolvePublicBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
        {
            return _options.PublicBaseUrl.TrimEnd('/');
        }

        HttpRequest? request = _httpContextAccessor.HttpContext?.Request;
        if (request is not null)
        {
            return $"{request.Scheme}://{request.Host}";
        }

        throw new InvalidOperationException(
            "PublicBaseUrl is not configured and no HTTP request is available to build asset URLs.");
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
