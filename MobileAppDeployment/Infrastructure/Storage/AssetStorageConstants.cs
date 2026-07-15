namespace MobileAppDeployment.Infrastructure.Storage;

/// <summary>
/// Canonical asset key constants used throughout the application to identify
/// specific uploaded files when saving to storage and dispatching to GitHub.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why constants?</strong>
/// The strings <c>"logo"</c>, <c>"splash"</c>, <c>"mobile-app-icon"</c> etc. appeared
/// as literals in the controller, the workflow orchestration service, and the asset
/// storage service — any typo or rename would break the upload silently. Centralizing
/// them here means:
/// <list type="bullet">
///   <item>A rename is one change in one file (the compiler finds all usages).</item>
///   <item>A typo causes a compile error, not a runtime file-not-found.</item>
///   <item>New developers can discover all asset keys from one place.</item>
/// </list>
/// </para>
/// <para>
/// <strong>Key format:</strong> lowercase with hyphens, matching the file name fragment
/// written to disk (e.g. <c>mobile-app-icon.png</c>).
/// </para>
/// </remarks>
public static class AssetStorageConstants
{
    // ── Deployment asset keys ─────────────────────────────────────────────
    // These are used as the file name base when storing client-uploaded assets.

    /// <summary>
    /// Mobile app icon — 512 × 512 px PNG or JPEG (≤ 1 MB).
    /// Stored as: <c>uploads/{deploymentId}/mobile-app-icon.{ext}</c>
    /// </summary>
    public const string MobileAppIcon = "mobile-app-icon";

    /// <summary>
    /// Launch (splash) image — 2732 × 2732 px PNG.
    /// Stored as: <c>uploads/{deploymentId}/launch-image.png</c>
    /// </summary>
    public const string LaunchImage = "launch-image";

    /// <summary>
    /// Google Play store icon — 512 × 512 px PNG.
    /// Stored as: <c>uploads/{deploymentId}/store-icon.png</c>
    /// </summary>
    public const string StoreIcon = "store-icon";

    /// <summary>
    /// Google Play feature graphic / banner — 1024 × 500 px PNG or JPEG (≤ 15 MB).
    /// Stored as: <c>uploads/{deploymentId}/feature-graphic.{ext}</c>
    /// </summary>
    public const string FeatureGraphic = "feature-graphic";

    /// <summary>
    /// iOS Firebase configuration file (<c>GoogleService-Info.plist</c>).
    /// Stored as: <c>uploads/{deploymentId}/firebase-ios-config.plist</c>
    /// </summary>
    public const string FirebaseIosConfig = "firebase-ios-config";

    /// <summary>
    /// Android Firebase configuration file (<c>google-services.json</c>).
    /// Stored as: <c>uploads/{deploymentId}/firebase-android-config.json</c>
    /// </summary>
    public const string FirebaseAndroidConfig = "firebase-android-config";

    /// <summary>
    /// Google Play service-account JSON key for automated store API access.
    /// Stored as: <c>uploads/{deploymentId}/play-store-key.json</c>
    /// </summary>
    public const string PlayStoreKey = "play-store-key";

    /// <summary>
    /// Apple App Store Connect API private key (<c>.p8</c> file).
    /// Stored as: <c>uploads/{deploymentId}/apple-auth-key.p8</c>
    /// </summary>
    public const string AppleAuthKey = "apple-auth-key";

    // ── Workflow asset keys ───────────────────────────────────────────────
    // These keys are used by WorkflowAssetStorageService when copying assets
    // from the deployment folder to the public workflow-assets folder.

    /// <summary>
    /// Logo key used in workflow dispatch inputs (maps to <see cref="MobileAppIcon"/>).
    /// </summary>
    public const string WorkflowLogo = "logo";

    /// <summary>
    /// Splash key used in workflow dispatch inputs (maps to <see cref="LaunchImage"/>).
    /// </summary>
    public const string WorkflowSplash = "splash";

    // ── Upload directory name ─────────────────────────────────────────────

    /// <summary>
    /// Root folder under <c>wwwroot</c> where all deployment assets are stored.
    /// </summary>
    public const string UploadsRoot = "uploads";

    /// <summary>
    /// Subfolder under <c>uploads</c> used for publicly-accessible workflow assets
    /// that GitHub Actions runners download during a workflow run.
    /// </summary>
    public const string WorkflowAssetsFolder = "workflow-assets";
}
