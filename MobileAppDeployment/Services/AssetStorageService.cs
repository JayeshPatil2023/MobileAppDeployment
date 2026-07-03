namespace MobileAppDeployment.Services;

public class AssetStorageService : IAssetStorageService
{
    private readonly IWebHostEnvironment _environment;

    public AssetStorageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string?> SaveAssetAsync(int deploymentId, IFormFile file, string assetKey, IEnumerable<string> allowedExtensions)
    {
        if (file.Length == 0)
        {
            return null;
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = allowedExtensions.Select(x => x.ToLowerInvariant()).ToHashSet();
        if (!allowed.Contains(extension))
        {
            throw new InvalidOperationException($"Invalid file type for {assetKey}. Allowed: {string.Join(", ", allowed)}.");
        }

        var uploadDirectory = GetDeploymentDirectory(deploymentId);
        Directory.CreateDirectory(uploadDirectory);

        var fileName = $"{assetKey}{extension}";
        var physicalPath = Path.Combine(uploadDirectory, fileName);

        await using var stream = new FileStream(physicalPath, FileMode.Create);
        await file.CopyToAsync(stream);

        return $"/uploads/{deploymentId}/{fileName}";
    }

    public Task DeleteDeploymentAssetsAsync(int deploymentId)
    {
        var uploadDirectory = GetDeploymentDirectory(deploymentId);
        if (Directory.Exists(uploadDirectory))
        {
            Directory.Delete(uploadDirectory, recursive: true);
        }

        return Task.CompletedTask;
    }

    private string GetDeploymentDirectory(int deploymentId) =>
        Path.Combine(_environment.WebRootPath, "uploads", deploymentId.ToString());
}
