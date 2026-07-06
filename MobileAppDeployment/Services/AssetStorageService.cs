namespace MobileAppDeployment.Services;

public class AssetStorageService : IAssetStorageService
{
    private readonly IBlobService _blobService;
    private readonly string _containerName;

    public AssetStorageService(IBlobService blobService, IConfiguration configuration)
    {
        _blobService = blobService;
        _containerName = (configuration["AzureBlobStorage:ClientAssetsContainerName"]
            ?? throw new InvalidOperationException("AzureBlobStorage:ClientAssetsContainerName is not configured."))
            .ToLowerInvariant();
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

        var fileName = $"{assetKey}{extension}";
        var folderName = deploymentId.ToString();

        var blobUrl = await _blobService.UploadFileAsync(file, _containerName, fileName, folderName);
        if (string.IsNullOrWhiteSpace(blobUrl))
        {
            throw new InvalidOperationException($"Failed to upload {assetKey} to blob storage.");
        }

        return blobUrl;
    }

    public Task DeleteDeploymentAssetsAsync(int deploymentId)
    {
        return _blobService.DeleteFolderAsync(_containerName, deploymentId.ToString());
    }
}
