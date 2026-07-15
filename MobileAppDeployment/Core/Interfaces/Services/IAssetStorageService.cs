namespace MobileAppDeployment.Core.Interfaces.Services;

public interface IAssetStorageService
{
    Task<string?> SaveAssetAsync(int deploymentId, IFormFile file, string assetKey, IEnumerable<string> allowedExtensions);
    Task DeleteDeploymentAssetsAsync(int deploymentId);
}
