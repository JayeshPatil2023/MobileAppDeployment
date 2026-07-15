using MobileAppDeployment.Infrastructure.Storage;

namespace MobileAppDeployment.Application.AssetUpload;

/// <summary>
/// Command object carrying a deployment id and uploaded asset files keyed by storage key.
/// </summary>
/// <param name="DeploymentId">Primary key of the deployment receiving uploads.</param>
/// <param name="Files">Map of asset storage keys to optional uploaded files.</param>
public record AssetUploadCommand(int DeploymentId, Dictionary<string, IFormFile?> Files)
{
    /// <summary>
    /// Builds an upload command from the standard Create/Edit form file parameters.
    /// </summary>
    public static AssetUploadCommand FromFormFiles(
        int deploymentId,
        IFormFile? mobileAppIconFile,
        IFormFile? launchImageFile,
        IFormFile? storeIconFile,
        IFormFile? featureGraphicFile,
        IFormFile? firebaseIosConfigFile,
        IFormFile? firebaseAndroidConfigFile,
        IFormFile? playStoreKeyFile,
        IFormFile? appleAuthKeyFile) =>
        new(deploymentId, new Dictionary<string, IFormFile?>
        {
            [AssetStorageConstants.MobileAppIcon] = mobileAppIconFile,
            [AssetStorageConstants.LaunchImage] = launchImageFile,
            [AssetStorageConstants.StoreIcon] = storeIconFile,
            [AssetStorageConstants.FeatureGraphic] = featureGraphicFile,
            ["GoogleService-Info"] = firebaseIosConfigFile,
            ["google-services"] = firebaseAndroidConfigFile,
            [AssetStorageConstants.PlayStoreKey] = playStoreKeyFile,
            ["AuthKey"] = appleAuthKeyFile
        });
}
