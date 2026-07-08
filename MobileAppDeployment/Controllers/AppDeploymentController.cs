using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using MobileAppDeployment.Models;
using MobileAppDeployment.Services;
using MobileAppDeployment.Services.GitHub;

namespace MobileAppDeployment.Controllers;

/// <summary>
/// MVC controller for app deployment CRUD and base workflow triggering.
/// </summary>
public class AppDeploymentController : Controller
{
    private static readonly string[] PngOnly = [".png"];
    private static readonly string[] PngOrJpeg = [".png", ".jpg", ".jpeg"];
    private static readonly string[] PlistOnly = [".plist"];
    private static readonly string[] JsonOnly = [".json"];

    private readonly IAppDeploymentService _service;
    private readonly IAssetStorageService _assetStorage;
    private readonly IWorkflowOrchestrationService _workflowOrchestration;
    private readonly ILogger<AppDeploymentController> _logger;

    /// <summary>
    /// Creates a new controller instance.
    /// </summary>
    public AppDeploymentController(
        IAppDeploymentService service,
        IAssetStorageService assetStorage,
        IWorkflowOrchestrationService workflowOrchestration,
        ILogger<AppDeploymentController> logger)
    {
        _service = service;
        _assetStorage = assetStorage;
        _workflowOrchestration = workflowOrchestration;
        _logger = logger;
    }

    /// <summary>
    /// Lists saved deployments.
    /// </summary>
    public async Task<IActionResult> Index()
    {
        IEnumerable<AppDeployment> deployments = await _service.GetAllAsync();
        return View(deployments);
    }

    /// <summary>
    /// Shows the New App Deployment form.
    /// </summary>
    public IActionResult Create()
    {
        return View(new AppDeployment());
    }

    /// <summary>
    /// Saves deployment details, then triggers the base GitHub Actions workflow.
    /// </summary>
    /// <remarks>
    /// Field mapping to workflow inputs:
    /// AppName → client_name,
    /// Mobile App Icon → logo,
    /// Launch Image → splash,
    /// IosBundleId → app_bundle_id,
    /// OneSignalAppId → app_id,
    /// OneSignalSenderId → project_id.
    /// </remarks>
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
        IFormFile? firebaseAndroidConfigFile,
        CancellationToken cancellationToken)
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
            await SaveUploadedFilesAsync(
                id,
                model,
                mobileAppIconFile,
                launchImageFile,
                storeIconFile,
                featureGraphicFile,
                firebaseIosConfigFile,
                firebaseAndroidConfigFile);
            await _service.UpdateAsync(model);

            // DB save first, then dispatch the base GitHub Actions workflow.
            string jobId = await _workflowOrchestration.StartWorkflowJobAsync(
                new WorkflowDispatchRequest
                {
                    ClientName = model.AppName,
                    LogoFile = mobileAppIconFile!,
                    SplashFile = launchImageFile!,
                    AppBundleId = model.IosBundleId,
                    AppId = model.OneSignalAppId,
                    ProjectId = model.OneSignalSenderId,
                    SavedMessage = "App deployment details saved successfully. Triggering base workflow..."
                },
                cancellationToken);

            return RedirectToAction(nameof(WorkflowProgress), new { jobId });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    /// <summary>
    /// Shows live progress while the base workflow is being triggered (with retries).
    /// </summary>
    public IActionResult WorkflowProgress(string jobId)
    {
        WorkflowJobState? job = _workflowOrchestration.GetJob(jobId);
        if (job is null)
        {
            return NotFound();
        }

        return View(new WorkflowProgressViewModel
        {
            JobId = job.JobId,
            ClientName = job.ClientName,
            SavedMessage = job.SavedMessage
        });
    }

    /// <summary>
    /// JSON endpoint polled by the workflow progress UI.
    /// </summary>
    [HttpGet]
    public IActionResult WorkflowStatus(string jobId)
    {
        WorkflowJobState? job = _workflowOrchestration.GetJob(jobId);
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
            clientName = job.ClientName
        });
    }

    /// <summary>
    /// Shows a single deployment.
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        AppDeployment? deployment = await _service.GetByIdAsync(id);
        if (deployment == null)
        {
            return NotFound();
        }

        return View(deployment);
    }

    /// <summary>
    /// Shows the edit form.
    /// </summary>
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

    /// <summary>
    /// Updates an existing deployment (does not re-trigger the base workflow).
    /// </summary>
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

    /// <summary>
    /// Confirms deletion.
    /// </summary>
    public async Task<IActionResult> Delete(int id)
    {
        AppDeployment? deployment = await _service.GetByIdAsync(id);
        if (deployment == null)
        {
            return NotFound();
        }

        return View(deployment);
    }

    /// <summary>
    /// Deletes a deployment and its stored assets.
    /// </summary>
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

    /// <summary>
    /// Validates required Create-form file uploads.
    /// </summary>
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

    /// <summary>
    /// Preserves existing asset paths and secrets when the edit form leaves them blank.
    /// </summary>
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

    /// <summary>
    /// Persists uploaded form files under wwwroot and writes relative paths onto the model.
    /// </summary>
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
