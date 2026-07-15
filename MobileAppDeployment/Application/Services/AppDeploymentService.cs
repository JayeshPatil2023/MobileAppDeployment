using MobileAppDeployment.Application.AssetUpload;
using MobileAppDeployment.Core.Domain.Entities;
using MobileAppDeployment.Core.Interfaces.Repositories;
using MobileAppDeployment.Core.Interfaces.Services;
using MobileAppDeployment.Core.Models.ViewModels;

namespace MobileAppDeployment.Application.Services;

/// <summary>
/// Default implementation of <see cref="IAppDeploymentService"/>.
/// </summary>
public class AppDeploymentService : IAppDeploymentService
{
    private readonly IAppDeploymentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFormAccessTokenService _formAccessTokenService;
    private readonly IAssetUploadStrategy _assetUploadStrategy;
    private readonly IAssetStorageService _assetStorage;
    private readonly ISecretProtectionService _secretProtection;

    /// <summary>
    /// Creates the service with repository and asset dependencies.
    /// </summary>
    public AppDeploymentService(
        IAppDeploymentRepository repository,
        IUnitOfWork unitOfWork,
        IFormAccessTokenService formAccessTokenService,
        IAssetUploadStrategy assetUploadStrategy,
        IAssetStorageService assetStorage,
        ISecretProtectionService secretProtection)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _formAccessTokenService = formAccessTokenService;
        _assetUploadStrategy = assetUploadStrategy;
        _assetStorage = assetStorage;
        _secretProtection = secretProtection;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<AppDeployment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IEnumerable<AppDeployment> deployments = await _repository.GetAllAsync(cancellationToken);
        foreach (AppDeployment deployment in deployments)
        {
            DecryptSecrets(deployment);
        }

        return deployments;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AppDeploymentListItemViewModel>> GetListItemsAsync(
        CancellationToken cancellationToken = default)
    {
        IEnumerable<AppDeployment> deployments = await GetAllAsync(cancellationToken);
        return deployments.Select(AppDeploymentListItemViewModel.FromEntity).ToList();
    }

    /// <inheritdoc />
    public async Task<AppDeployment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        AppDeployment? deployment = await _repository.GetByIdAsync(id, cancellationToken);
        if (deployment is not null)
        {
            DecryptSecrets(deployment);
        }

        return deployment;
    }

    /// <inheritdoc />
    public async Task<int> CreateAsync(AppDeployment appDeployment, CancellationToken cancellationToken = default)
    {
        EncryptSecrets(appDeployment);
        await _repository.InsertAsync(appDeployment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return appDeployment.Id;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(AppDeployment appDeployment, CancellationToken cancellationToken = default)
    {
        EncryptSecrets(appDeployment);
        bool updated = await _repository.UpdateAsync(appDeployment, cancellationToken);
        if (updated)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return updated;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        bool deleted = await _repository.DeleteAsync(id, cancellationToken);
        if (deleted)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _assetStorage.DeleteDeploymentAssetsAsync(id);
        }

        return deleted;
    }

    /// <inheritdoc />
    public Task SaveAssetsAsync(
        AssetUploadCommand command,
        AppDeployment deployment,
        CancellationToken cancellationToken = default) =>
        _assetUploadStrategy.ApplyUploadsAsync(command, deployment, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> SaveDraftWithAssetsAsync(
        AppDeployment entity,
        Func<int, AssetUploadCommand> uploadsFactory,
        string? formToken,
        bool isCreate,
        CancellationToken cancellationToken = default)
    {
        int deploymentId;
        if (isCreate)
        {
            deploymentId = await CreateAsync(entity, cancellationToken);
            entity.Id = deploymentId;
            if (!string.IsNullOrWhiteSpace(formToken))
            {
                await _formAccessTokenService.MarkSubmittedAsync(formToken, deploymentId);
            }
        }
        else
        {
            deploymentId = entity.Id;
        }

        AssetUploadCommand uploadCommand = uploadsFactory(deploymentId);
        await SaveAssetsAsync(uploadCommand, entity, cancellationToken);

        // Persist path updates (and on edit, the rest of the mapped fields).
        return await UpdateAsync(entity, cancellationToken);
    }

    /// <summary>
    /// Encrypts sensitive fields before persistence.
    /// </summary>
    private void EncryptSecrets(AppDeployment deployment)
    {
        if (!string.IsNullOrWhiteSpace(deployment.OneSignalRestApiKey))
        {
            deployment.OneSignalRestApiKeyEncrypted = _secretProtection.Protect(deployment.OneSignalRestApiKey);
        }

        deployment.OneSignalRestApiKey = string.Empty;
    }

    /// <summary>
    /// Decrypts sensitive fields after loading from the database.
    /// </summary>
    private void DecryptSecrets(AppDeployment deployment)
    {
        if (!string.IsNullOrWhiteSpace(deployment.OneSignalRestApiKeyEncrypted))
        {
            deployment.OneSignalRestApiKey = _secretProtection.Unprotect(deployment.OneSignalRestApiKeyEncrypted);
        }
    }
}
