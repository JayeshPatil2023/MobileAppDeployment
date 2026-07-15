using MobileAppDeployment.Application.AssetUpload;
using MobileAppDeployment.Core.Domain.Entities;
using MobileAppDeployment.Core.Models.ViewModels;

namespace MobileAppDeployment.Core.Interfaces.Services;

/// <summary>
/// Application service for deployment CRUD and asset upload orchestration.
/// </summary>
public interface IAppDeploymentService
{
    /// <summary>Lists all deployments ordered by creation date descending.</summary>
    Task<IEnumerable<AppDeployment>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Loads a deployment by primary key.</summary>
    Task<AppDeployment?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Maps deployments to list-item view models for the Index page.</summary>
    Task<IReadOnlyList<AppDeploymentListItemViewModel>> GetListItemsAsync(CancellationToken cancellationToken = default);

    /// <summary>Inserts a new deployment row.</summary>
    Task<int> CreateAsync(AppDeployment appDeployment, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing deployment row.</summary>
    Task<bool> UpdateAsync(AppDeployment appDeployment, CancellationToken cancellationToken = default);

    /// <summary>Deletes a deployment row and its stored assets.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves uploaded assets for a deployment and persists path updates on the entity.
    /// </summary>
    Task SaveAssetsAsync(
        AssetUploadCommand command,
        AppDeployment deployment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a draft deployment, optionally linking a form-access token on create,
    /// then applies asset uploads and persists path updates.
    /// </summary>
    /// <param name="entity">Mapped domain entity ready for persistence.</param>
    /// <param name="uploadsFactory">
    /// Builds the upload command after the deployment id is known (create assigns the id).
    /// </param>
    /// <param name="formToken">Required on create so the token is linked to the new row.</param>
    /// <param name="isCreate">When true, insert then link token; otherwise update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>False when an update targets a missing row; otherwise true.</returns>
    Task<bool> SaveDraftWithAssetsAsync(
        AppDeployment entity,
        Func<int, AssetUploadCommand> uploadsFactory,
        string? formToken,
        bool isCreate,
        CancellationToken cancellationToken = default);
}
