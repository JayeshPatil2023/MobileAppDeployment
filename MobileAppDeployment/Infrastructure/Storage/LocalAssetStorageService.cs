namespace MobileAppDeployment.Infrastructure.Storage;

/// <summary>
/// Saves client-uploaded asset files under <c>wwwroot/uploads/{deploymentId}/</c>
/// and returns the relative URL path for the stored file.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Security:</strong> This service defends against path traversal by
/// canonicalizing the destination path with <see cref="Path.GetFullPath"/> and
/// verifying it remains inside the permitted upload directory before writing.
/// User-supplied filenames are never used as-is; only the extension is extracted
/// and that is validated against an explicit allow-list before the file is saved.
/// </para>
/// <para>
/// <strong>Scalability:</strong> This is the <em>local file system</em> implementation
/// of <see cref="IAssetStorageService"/>. To store files in cloud storage (Azure Blob,
/// S3, etc.) implement the same interface and swap the DI registration without
/// changing any controller or service code.
/// </para>
/// </remarks>
public class LocalAssetStorageService : IAssetStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<LocalAssetStorageService> _logger;

    /// <summary>Creates the local asset storage service.</summary>
    public LocalAssetStorageService(
        IWebHostEnvironment environment,
        ILogger<LocalAssetStorageService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string?> SaveAssetAsync(
        int deploymentId,
        IFormFile file,
        string assetKey,
        IEnumerable<string> allowedExtensions)
    {
        if (file is null || file.Length == 0)
        {
            return null;
        }

        // ── Validate extension against allow-list ─────────────────────────
        // Extract the extension from the user-supplied filename.
        // We use the extension only — never the full filename — so path separators
        // embedded in the filename cannot be used for traversal.
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        HashSet<string> allowed = allowedExtensions
            .Select(x => x.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!allowed.Contains(extension))
        {
            throw new InvalidOperationException(
                $"Invalid file type for {assetKey}. Allowed: {string.Join(", ", allowed)}.");
        }

        // ── Build and validate the destination path ───────────────────────
        // The upload directory is rooted at WebRootPath which is fully controlled
        // by the server. The final path is canonicalized and verified to be a
        // child of the permitted directory before any file is written.
        string uploadDirectory = GetDeploymentDirectory(deploymentId);
        Directory.CreateDirectory(uploadDirectory);

        // assetKey comes from a server-controlled constant (never from user input)
        // so we only need to validate that the canonicalized path stays inside
        // the expected directory.
        string fileName = $"{assetKey}{extension}";
        string requestedPath = Path.Combine(uploadDirectory, fileName);

        // Canonicalize to resolve any .. or symlink sequences.
        string canonicalPath = Path.GetFullPath(requestedPath);
        string canonicalDirectory = Path.GetFullPath(uploadDirectory);

        if (!canonicalPath.StartsWith(canonicalDirectory + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            // This should never happen because assetKey is server-controlled,
            // but defense-in-depth: log and reject.
            _logger.LogCritical(
                "Path traversal attempt detected. Requested path {RequestedPath} is outside {Directory}.",
                canonicalPath,
                canonicalDirectory);

            throw new InvalidOperationException(
                $"Invalid asset path for {assetKey}. The file cannot be stored at the requested location.");
        }

        // ── Write the file ─────────────────────────────────────────────────
        await using FileStream stream = new(canonicalPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(stream);

        _logger.LogInformation(
            "Saved asset {AssetKey}{Extension} for deployment {DeploymentId}.",
            assetKey, extension, deploymentId);

        // Return the web-relative URL (rooted at wwwroot).
        return $"/uploads/{deploymentId}/{fileName}";
    }

    /// <inheritdoc />
    public Task DeleteDeploymentAssetsAsync(int deploymentId)
    {
        string uploadDirectory = GetDeploymentDirectory(deploymentId);

        if (Directory.Exists(uploadDirectory))
        {
            Directory.Delete(uploadDirectory, recursive: true);
            _logger.LogInformation("Deleted asset directory for deployment {DeploymentId}.", deploymentId);
        }

        return Task.CompletedTask;
    }

    // ── Private helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Returns the physical path of the upload directory for a specific deployment.
    /// </summary>
    private string GetDeploymentDirectory(int deploymentId) =>
        Path.Combine(_environment.WebRootPath, "uploads", deploymentId.ToString());
}
