using MobileAppDeployment.Core.Domain.Entities;

namespace MobileAppDeployment.Application.AssetUpload;

/// <summary>
/// Strategy for persisting uploaded deployment assets and updating the entity paths.
/// </summary>
public interface IAssetUploadStrategy
{
    /// <summary>
    /// Saves each non-null uploaded file and writes the resulting paths onto the deployment entity.
    /// </summary>
    /// <param name="command">Upload command with deployment id and file dictionary.</param>
    /// <param name="deployment">Deployment entity whose path properties will be updated.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ApplyUploadsAsync(
        AssetUploadCommand command,
        AppDeployment deployment,
        CancellationToken cancellationToken = default);
}
