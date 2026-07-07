using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using MobileAppDeployment.Models;
using MobileAppDeployment.Services;
using MobileAppDeployment.Services.GitHub;

namespace MobileAppDeployment.Controllers;

public class AppDeploymentController : Controller
{
    private static readonly string[] PngOnly = [".png"];
    private static readonly string[] PngOrJpeg = [".png", ".jpg", ".jpeg"];
    private static readonly string[] PlistOnly = [".plist"];
    private static readonly string[] JsonOnly = [".json"];

    private readonly IAppDeploymentService _service;
    private readonly IAssetStorageService _assetStorage;
    private readonly IGitHubRepositoryService _gitHubRepositoryService;
    private readonly IGitHubWorkflowDispatchService _gitHubWorkflowDispatchService;
    private readonly IWorkflowAssetStorageService _workflowAssetStorage;
    private readonly IRepoMergeService _repoMergeService;
    private readonly RepoMergeOptions _repoMergeOptions;
    private readonly ILogger<AppDeploymentController> _logger;

    public AppDeploymentController(
        IAppDeploymentService service,
        IAssetStorageService assetStorage,
        IGitHubRepositoryService gitHubRepositoryService,
        IGitHubWorkflowDispatchService gitHubWorkflowDispatchService,
        IWorkflowAssetStorageService workflowAssetStorage,
        IRepoMergeService repoMergeService,
        Microsoft.Extensions.Options.IOptions<RepoMergeOptions> repoMergeOptions,
        ILogger<AppDeploymentController> logger)
    {
        _service = service;
        _assetStorage = assetStorage;
        _gitHubRepositoryService = gitHubRepositoryService;
        _gitHubWorkflowDispatchService = gitHubWorkflowDispatchService;
        _workflowAssetStorage = workflowAssetStorage;
        _repoMergeService = repoMergeService;
        _repoMergeOptions = repoMergeOptions.Value;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        IEnumerable<AppDeployment> deployments = await _service.GetAllAsync();
        return View(deployments);
    }

    public IActionResult Create()
    {
        return View(new AppDeployment());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> Create(
        AppDeployment model,
        IFormFile? mobileAppIconFile,
        IFormFile? launchImageFile,
        IFormFile? storeIconFile,
        IFormFile? featureGraphicFile,
        IFormFile? firebaseIosConfigFile,
        IFormFile? firebaseAndroidConfigFile)
    {
        ValidateRequiredFiles(
            ModelState,
            isEdit: false,
            mobileAppIconFile,
            launchImageFile,
            storeIconFile,
            featureGraphicFile,
            firebaseIosConfigFile,
            firebaseAndroidConfigFile);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            int id = await _service.CreateAsync(model);
            await SaveUploadedFilesAsync(id, model, mobileAppIconFile, launchImageFile, storeIconFile, featureGraphicFile, firebaseIosConfigFile, firebaseAndroidConfigFile);
            await _service.UpdateAsync(model);

            GitHubRepositoryResult gitHubResult = await _gitHubRepositoryService.CreateClientRepositoryAsync(model);
            if (gitHubResult.Success)
            {
                string repoCreatedMessage = $"App deployment details saved successfully. GitHub repository created: {gitHubResult.HtmlUrl}";

                if (_repoMergeOptions.Enabled)
                {
                    string clientRepoName = gitHubResult.RepositoryName ?? model.AppName;
                    string jobId = _repoMergeService.StartMergeJob(
                        clientRepoName,
                        model.AppName,
                        gitHubResult.HtmlUrl);

                    TempData["RepoCreatedMessage"] = repoCreatedMessage;
                    return RedirectToAction(nameof(MergeProgress), new { jobId });
                }

                TempData["SuccessMessage"] = repoCreatedMessage;
            }
            else
            {
                TempData["SuccessMessage"] = "App deployment details saved successfully.";
                if (!string.IsNullOrWhiteSpace(gitHubResult.ErrorMessage))
                {
                    TempData["WarningMessage"] = $"GitHub repository was not created: {gitHubResult.ErrorMessage}";
                    _logger.LogWarning(
                        "Deployment {DeploymentId} saved but GitHub repo creation failed: {Error}",
                        id,
                        gitHubResult.ErrorMessage);
                }
            }
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Shows live progress while source code is merged into the newly created client repository.
    /// </summary>
    public IActionResult MergeProgress(string jobId)
    {
        RepoMergeJobState? job = _repoMergeService.GetJob(jobId);
        if (job is null)
        {
            return NotFound();
        }

        MergeProgressViewModel viewModel = new()
        {
            JobId = job.JobId,
            AppName = job.AppName,
            ClientRepoName = job.ClientRepoName,
            RepositoryUrl = job.RepositoryUrl,
            RepoCreatedMessage = TempData["RepoCreatedMessage"]?.ToString()
        };

        return View(viewModel);
    }

    /// <summary>
    /// JSON endpoint polled by the merge progress UI for live status updates.
    /// </summary>
    [HttpGet]
    public IActionResult MergeStatus(string jobId)
    {
        RepoMergeJobState? job = _repoMergeService.GetJob(jobId);
        if (job is null)
        {
            return NotFound();
        }

        return Json(new
        {
            jobId = job.JobId,
            status = job.Status.ToString(),
            percentComplete = job.PercentComplete,
            currentMessage = job.CurrentMessage,
            attempt = job.Attempt,
            maxRetries = job.MaxRetries,
            errorMessage = job.ErrorMessage,
            isTerminal = job.IsTerminal,
            repositoryUrl = job.RepositoryUrl,
            clientRepoName = job.ClientRepoName
        });
    }

    /// <summary>
    /// Uploads logo/splash assets, stores them on the server, and triggers the GitHub Actions workflow.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> TriggerWorkflow(
        string? clientName,
        IFormFile? logoFile,
        IFormFile? splashFile,
        CancellationToken cancellationToken)
    {
        if (logoFile is null || logoFile.Length == 0)
        {
            TempData["WarningMessage"] = "Logo image is required to trigger the workflow.";
            return RedirectToAction(nameof(Index));
        }

        if (splashFile is null || splashFile.Length == 0)
        {
            TempData["WarningMessage"] = "Splash image is required to trigger the workflow.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(clientName))
        {
            TempData["WarningMessage"] = "Client name is required to trigger the workflow.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            string logoUrl = await _workflowAssetStorage.SaveAndGetPublicUrlAsync(
                logoFile,
                clientName,
                "logo",
                PngOnly);

            string splashUrl = await _workflowAssetStorage.SaveAndGetPublicUrlAsync(
                splashFile,
                clientName,
                "splash",
                PngOnly);

            GitHubWorkflowDispatchResult result = await _gitHubWorkflowDispatchService.TriggerAsync(
                clientName,
                logoUrl,
                splashUrl,
                cancellationToken);

            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["WarningMessage"] = result.Message;
            }
        }
        catch (InvalidOperationException ex)
        {
            TempData["WarningMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id)
    {
        AppDeployment? deployment = await _service.GetByIdAsync(id);
        if (deployment == null)
        {
            return NotFound();
        }

        return View(deployment);
    }

    public async Task<IActionResult> Edit(int id)
    {
        AppDeployment? deployment = await _service.GetByIdAsync(id);
        if (deployment == null)
        {
            return NotFound();
        }

        deployment.OneSignalRestApiKey = string.Empty;
        return View(deployment);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> Edit(
        int id,
        AppDeployment model,
        IFormFile? mobileAppIconFile,
        IFormFile? launchImageFile,
        IFormFile? storeIconFile,
        IFormFile? featureGraphicFile,
        IFormFile? firebaseIosConfigFile,
        IFormFile? firebaseAndroidConfigFile)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        AppDeployment? existing = await _service.GetByIdAsync(id);
        if (existing == null)
        {
            return NotFound();
        }

        PreserveExistingPaths(model, existing);

        if (!string.IsNullOrWhiteSpace(model.OneSignalRestApiKey))
        {
            ModelState.Remove(nameof(AppDeployment.OneSignalRestApiKey));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await SaveUploadedFilesAsync(id, model, mobileAppIconFile, launchImageFile, storeIconFile, featureGraphicFile, firebaseIosConfigFile, firebaseAndroidConfigFile);
            bool updated = await _service.UpdateAsync(model);
            if (!updated)
            {
                return NotFound();
            }
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        TempData["SuccessMessage"] = "App deployment details updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        AppDeployment? deployment = await _service.GetByIdAsync(id);
        if (deployment == null)
        {
            return NotFound();
        }

        return View(deployment);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        bool deleted = await _service.DeleteAsync(id);
        if (!deleted)
        {
            return NotFound();
        }

        await _assetStorage.DeleteDeploymentAssetsAsync(id);

        TempData["SuccessMessage"] = "App deployment deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    private static void ValidateRequiredFiles(
        ModelStateDictionary modelState,
        bool isEdit,
        IFormFile? mobileAppIconFile,
        IFormFile? launchImageFile,
        IFormFile? storeIconFile,
        IFormFile? featureGraphicFile,
        IFormFile? firebaseIosConfigFile,
        IFormFile? firebaseAndroidConfigFile)
    {
        if (isEdit)
        {
            return;
        }

        if (mobileAppIconFile is null || mobileAppIconFile.Length == 0)
        {
            modelState.AddModelError("mobileAppIconFile", "Mobile app icon is required.");
        }

        if (launchImageFile is null || launchImageFile.Length == 0)
        {
            modelState.AddModelError("launchImageFile", "Launch image is required.");
        }

        if (storeIconFile is null || storeIconFile.Length == 0)
        {
            modelState.AddModelError("storeIconFile", "Store icon is required.");
        }

        if (featureGraphicFile is null || featureGraphicFile.Length == 0)
        {
            modelState.AddModelError("featureGraphicFile", "Feature graphic is required.");
        }

        if (firebaseIosConfigFile is null || firebaseIosConfigFile.Length == 0)
        {
            modelState.AddModelError("firebaseIosConfigFile", "GoogleService-Info.plist is required.");
        }

        if (firebaseAndroidConfigFile is null || firebaseAndroidConfigFile.Length == 0)
        {
            modelState.AddModelError("firebaseAndroidConfigFile", "google-services.json is required.");
        }
    }

    private static void PreserveExistingPaths(AppDeployment model, AppDeployment existing)
    {
        model.MobileAppIconPath ??= existing.MobileAppIconPath;
        model.LaunchImagePath ??= existing.LaunchImagePath;
        model.StoreIconPath ??= existing.StoreIconPath;
        model.FeatureGraphicPath ??= existing.FeatureGraphicPath;
        model.FirebaseIosConfigPath ??= existing.FirebaseIosConfigPath;
        model.FirebaseAndroidConfigPath ??= existing.FirebaseAndroidConfigPath;

        if (string.IsNullOrWhiteSpace(model.OneSignalRestApiKey))
        {
            model.OneSignalRestApiKey = existing.OneSignalRestApiKey;
        }
    }

    private async Task SaveUploadedFilesAsync(
        int deploymentId,
        AppDeployment model,
        IFormFile? mobileAppIconFile,
        IFormFile? launchImageFile,
        IFormFile? storeIconFile,
        IFormFile? featureGraphicFile,
        IFormFile? firebaseIosConfigFile,
        IFormFile? firebaseAndroidConfigFile)
    {
        if (mobileAppIconFile is { Length: > 0 })
        {
            model.MobileAppIconPath = await _assetStorage.SaveAssetAsync(deploymentId, mobileAppIconFile, "mobile-app-icon", PngOnly);
        }

        if (launchImageFile is { Length: > 0 })
        {
            model.LaunchImagePath = await _assetStorage.SaveAssetAsync(deploymentId, launchImageFile, "launch-image", PngOnly);
        }

        if (storeIconFile is { Length: > 0 })
        {
            model.StoreIconPath = await _assetStorage.SaveAssetAsync(deploymentId, storeIconFile, "store-icon", PngOnly);
        }

        if (featureGraphicFile is { Length: > 0 })
        {
            model.FeatureGraphicPath = await _assetStorage.SaveAssetAsync(deploymentId, featureGraphicFile, "feature-graphic", PngOrJpeg);
        }

        if (firebaseIosConfigFile is { Length: > 0 })
        {
            model.FirebaseIosConfigPath = await _assetStorage.SaveAssetAsync(deploymentId, firebaseIosConfigFile, "GoogleService-Info", PlistOnly);
        }

        if (firebaseAndroidConfigFile is { Length: > 0 })
        {
            model.FirebaseAndroidConfigPath = await _assetStorage.SaveAssetAsync(deploymentId, firebaseAndroidConfigFile, "google-services", JsonOnly);
        }
    }
}
