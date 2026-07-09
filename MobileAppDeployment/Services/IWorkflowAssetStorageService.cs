namespace MobileAppDeployment.Services;

/// <summary>
/// Stores logo/splash files for workflow dispatch and returns publicly reachable URLs.
/// </summary>
public interface IWorkflowAssetStorageService
{
    /// <summary>
    /// Saves an uploaded image under wwwroot and returns an absolute URL for GitHub Actions to download.
    /// </summary>
    /// <param name="file">Uploaded image file.</param>
    /// <param name="clientName">Client repository name used to group stored assets.</param>
    /// <param name="assetKey">Logical asset key (for example: logo, splash).</param>
    /// <param name="allowedExtensions">Permitted file extensions.</param>
    Task<string> SaveAndGetPublicUrlAsync(
        IFormFile file,
        string clientName,
        string assetKey,
        IEnumerable<string> allowedExtensions);

    /// <summary>
    /// Copies an already-saved deployment asset from <c>wwwroot</c> into the workflow-assets folder
    /// and returns an absolute public URL for GitHub Actions to download.
    /// </summary>
    /// <param name="relativeWebPath">
    /// Web-relative path stored on the deployment (for example <c>/uploads/12/mobile-app-icon.png</c>).
    /// </param>
    /// <param name="clientName">Client repository name used to group stored workflow assets.</param>
    /// <param name="assetKey">Logical workflow asset key (for example: logo, splash).</param>
    /// <param name="allowedExtensions">Permitted file extensions.</param>
    Task<string> PublishStoredFileAndGetPublicUrlAsync(
        string relativeWebPath,
        string clientName,
        string assetKey,
        IEnumerable<string> allowedExtensions);
}
