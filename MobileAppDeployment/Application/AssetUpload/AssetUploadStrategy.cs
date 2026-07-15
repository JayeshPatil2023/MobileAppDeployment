using MobileAppDeployment.Infrastructure.Storage;
using MobileAppDeployment.Core.Domain.Entities;
using MobileAppDeployment.Core.Interfaces.Services;

namespace MobileAppDeployment.Application.AssetUpload;

/// <summary>
/// Default asset upload strategy that saves files via <see cref="IAssetStorageService"/>.
/// </summary>
public class AssetUploadStrategy : IAssetUploadStrategy
{
    private static readonly string[] PngOnly = [".png"];
    private static readonly string[] PngOrJpeg = [".png", ".jpg", ".jpeg"];
    private static readonly string[] PlistOnly = [".plist"];
    private static readonly string[] JsonOnly = [".json"];
    private static readonly string[] P8Only = [".p8"];

    private static readonly Dictionary<string, string[]> AllowedExtensionsByKey =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [AssetStorageConstants.MobileAppIcon] = PngOrJpeg,
            [AssetStorageConstants.LaunchImage] = PngOnly,
            [AssetStorageConstants.StoreIcon] = PngOnly,
            [AssetStorageConstants.FeatureGraphic] = PngOrJpeg,
            ["GoogleService-Info"] = PlistOnly,
            ["google-services"] = JsonOnly,
            [AssetStorageConstants.PlayStoreKey] = JsonOnly,
            ["AuthKey"] = P8Only
        };

    private readonly IAssetStorageService _assetStorage;

    /// <summary>
    /// Creates the strategy with the asset storage dependency.
    /// </summary>
    public AssetUploadStrategy(IAssetStorageService assetStorage)
    {
        _assetStorage = assetStorage;
    }

    /// <inheritdoc />
    public async Task ApplyUploadsAsync(
        AssetUploadCommand command,
        AppDeployment deployment,
        CancellationToken cancellationToken = default)
    {
        foreach (KeyValuePair<string, IFormFile?> entry in command.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IFormFile? file = entry.Value;
            if (file is not { Length: > 0 })
            {
                continue;
            }

            if (!AllowedExtensionsByKey.TryGetValue(entry.Key, out string[]? allowedExtensions))
            {
                throw new InvalidOperationException($"Unknown asset key: {entry.Key}");
            }

            string? path = await _assetStorage.SaveAssetAsync(
                command.DeploymentId,
                file,
                entry.Key,
                allowedExtensions);

            if (path is null)
            {
                continue;
            }

            switch (entry.Key)
            {
                case AssetStorageConstants.MobileAppIcon:
                    deployment.MobileAppIconPath = path;
                    break;
                case AssetStorageConstants.LaunchImage:
                    deployment.LaunchImagePath = path;
                    break;
                case AssetStorageConstants.StoreIcon:
                    deployment.StoreIconPath = path;
                    break;
                case AssetStorageConstants.FeatureGraphic:
                    deployment.FeatureGraphicPath = path;
                    break;
                case "GoogleService-Info":
                    deployment.FirebaseIosConfigPath = path;
                    break;
                case "google-services":
                    deployment.FirebaseAndroidConfigPath = path;
                    break;
                case AssetStorageConstants.PlayStoreKey:
                    deployment.PlayStoreKeyPath = path;
                    break;
                case "AuthKey":
                    deployment.AppleAuthKeyPath = path;
                    break;
            }
        }
    }
}
