namespace MobileAppDeployment.Core.Domain.ValueObjects;

/// <summary>
/// Groups the eight relative asset path properties for a deployment without changing the DB schema.
/// </summary>
/// <remarks>
/// Paths remain stored as columns on <see cref="AppDeployment"/>. Use <see cref="From"/> / <see cref="ApplyTo"/>
/// to treat them as a conceptual group in application code.
/// </remarks>
public sealed class AssetPaths
{
    /// <summary>Relative URL path for the mobile app icon (PNG 1024×1024).</summary>
    public string? MobileAppIconPath { get; set; }

    /// <summary>Relative URL path for the launch / splash image.</summary>
    public string? LaunchImagePath { get; set; }

    /// <summary>Relative URL path for the store icon.</summary>
    public string? StoreIconPath { get; set; }

    /// <summary>Relative URL path for the feature graphic.</summary>
    public string? FeatureGraphicPath { get; set; }

    /// <summary>Relative URL path for the Play Store service-account key JSON.</summary>
    public string? PlayStoreKeyPath { get; set; }

    /// <summary>Relative URL path for the Apple AuthKey (.p8).</summary>
    public string? AppleAuthKeyPath { get; set; }

    /// <summary>Relative URL path for the Firebase iOS config (GoogleService-Info.plist).</summary>
    public string? FirebaseIosConfigPath { get; set; }

    /// <summary>Relative URL path for the Firebase Android config (google-services.json).</summary>
    public string? FirebaseAndroidConfigPath { get; set; }

    /// <summary>
    /// Creates an <see cref="AssetPaths"/> snapshot from an <see cref="AppDeployment"/> entity.
    /// </summary>
    public static AssetPaths From(AppDeployment deployment) => new()
    {
        MobileAppIconPath = deployment.MobileAppIconPath,
        LaunchImagePath = deployment.LaunchImagePath,
        StoreIconPath = deployment.StoreIconPath,
        FeatureGraphicPath = deployment.FeatureGraphicPath,
        PlayStoreKeyPath = deployment.PlayStoreKeyPath,
        AppleAuthKeyPath = deployment.AppleAuthKeyPath,
        FirebaseIosConfigPath = deployment.FirebaseIosConfigPath,
        FirebaseAndroidConfigPath = deployment.FirebaseAndroidConfigPath
    };

    /// <summary>
    /// Copies path values onto an <see cref="AppDeployment"/> entity (no EF owned-type / schema change).
    /// </summary>
    public void ApplyTo(AppDeployment deployment)
    {
        deployment.MobileAppIconPath = MobileAppIconPath;
        deployment.LaunchImagePath = LaunchImagePath;
        deployment.StoreIconPath = StoreIconPath;
        deployment.FeatureGraphicPath = FeatureGraphicPath;
        deployment.PlayStoreKeyPath = PlayStoreKeyPath;
        deployment.AppleAuthKeyPath = AppleAuthKeyPath;
        deployment.FirebaseIosConfigPath = FirebaseIosConfigPath;
        deployment.FirebaseAndroidConfigPath = FirebaseAndroidConfigPath;
    }
}
