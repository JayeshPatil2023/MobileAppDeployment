using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using MobileAppDeployment.Models;
using MobileAppDeployment.Services;
using MobileAppDeployment.Services.GitHub;

namespace MobileAppDeployment.Controllers;

/// <summary>
/// MVC controller for app deployment CRUD and explicit base workflow triggering.
/// </summary>
/// <remarks>
/// <para><strong>Save vs start:</strong> Create and Edit only persist deployment data and assets.
/// The GitHub Actions workflow starts only when the user clicks
/// <strong>Start App Deployment Process</strong> on the Edit form.</para>
/// <para>
/// Public Create access is token-gated: clients open <c>/AppDeployment/Form/{token}</c>.
/// Tokens are issued by admins via <c>POST /api/form-access-tokens</c>.
/// </para>
/// <para>
/// After the first successful Create submit, the same token URL opens the Edit form.
/// Index / Details / Delete remain available for in-app admin browsing (no login yet).
/// </para>
/// </remarks>
public class AppDeploymentController : Controller
{
    private static readonly string[] PngOnly = [".png"];
    private static readonly string[] PngOrJpeg = [".png", ".jpg", ".jpeg"];
    private static readonly string[] PlistOnly = [".plist"];
    private static readonly string[] JsonOnly = [".json"];

    private readonly IAppDeploymentService _service;
    private readonly IAssetStorageService _assetStorage;
    private readonly IWorkflowOrchestrationService _workflowOrchestration;
    private readonly IFormAccessTokenService _formAccessTokenService;
    private readonly ILogger<AppDeploymentController> _logger;

    /// <summary>
    /// Creates a new controller instance.
    /// </summary>
    public AppDeploymentController(
        IAppDeploymentService service,
        IAssetStorageService assetStorage,
        IWorkflowOrchestrationService workflowOrchestration,
        IFormAccessTokenService formAccessTokenService,
        ILogger<AppDeploymentController> logger)
    {
        _service = service;
        _assetStorage = assetStorage;
        _workflowOrchestration = workflowOrchestration;
        _formAccessTokenService = formAccessTokenService;
        _logger = logger;
    }

    /// <summary>
    /// Lists saved deployments (admin/internal view).
    /// </summary>
    public async Task<IActionResult> Index()
    {
        IEnumerable<AppDeployment> deployments = await _service.GetAllAsync();
        return View(deployments);
    }

    /// <summary>
    /// Entry point for a client magic-link: shows Create if the token is unused, otherwise Edit.
    /// </summary>
    /// <param name="token">Opaque form-access token from the shareable URL.</param>
    [HttpGet]
    [Route("AppDeployment/Form/{token}")]
    public async Task<IActionResult> Form(string token)
    {
        FormAccessToken? access = await _formAccessTokenService.ResolveActiveAsync(token);
        if (access is null)
        {
            return View("InvalidToken");
        }

        // Already submitted → edit the linked deployment with the same shareable URL.
        if (access.AppDeploymentId is int deploymentId)
        {
            AppDeployment? deployment = await _service.GetByIdAsync(deploymentId);
            if (deployment is null)
            {
                // Token still active but deployment was deleted — block until admin re-issues.
                _logger.LogWarning(
                    "Form token {TokenId} points at missing deployment {DeploymentId}.",
                    access.Id,
                    deploymentId);
                return View("InvalidToken");
            }

            deployment.OneSignalRestApiKey = string.Empty;
            ViewBag.FormToken = access.Token;
            ViewBag.DeploymentId = deployment.Id;
            return View("Edit", deployment);
        }

        // First visit — blank Create form with App Name prefilled from token issuance.
        var model = new AppDeployment
        {
            AppName = TruncateForAppName(access.ClientAppName)
        };
        ViewBag.FormToken = access.Token;
        ViewBag.DeploymentId = null;
        return View("Create", model);
    }

    /// <summary>
    /// Open Create without a token is disabled — clients must use the magic link.
    /// </summary>
    public IActionResult Create()
    {
        return View("InvalidToken");
    }

    /// <summary>
    /// Saves deployment details for a valid unused token. Does not start the GitHub Actions workflow.
    /// </summary>
    /// <remarks>
    /// After a successful save the client is redirected back to the same token URL, which now loads Edit.
    /// Workflow field mapping (used later by <see cref="StartDeployment"/>):
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
        string token,
        AppDeployment model,
        IFormFile? mobileAppIconFile,
        IFormFile? launchImageFile,
        IFormFile? storeIconFile,
        IFormFile? featureGraphicFile,
        IFormFile? firebaseIosConfigFile,
        IFormFile? firebaseAndroidConfigFile)
    {
        FormAccessToken? access = await _formAccessTokenService.ResolveActiveAsync(token);
        if (access is null)
        {
            return View("InvalidToken");
        }

        // Business rule: a token that already created a deployment cannot Create again.
        if (access.AppDeploymentId.HasValue)
        {
            return RedirectToAction(nameof(Form), new { token = access.Token });
        }

        ViewBag.FormToken = access.Token;

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

            // Link token → deployment so later Form visits open Edit instead of Create.
            await _formAccessTokenService.MarkSubmittedAsync(access.Token, id);

            TempData["SuccessMessage"] = "Deployment saved successfully.";
            return RedirectToAction(nameof(Form), new { token = access.Token });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    /// <summary>
    /// Explicitly starts the base GitHub Actions workflow for a saved deployment.
    /// </summary>
    /// <remarks>
    /// Uses the <strong>persisted</strong> deployment row (not unsaved form fields).
    /// Logo and splash are published from stored asset paths under <c>wwwroot/uploads/{id}/</c>.
    /// </remarks>
    /// <param name="id">Deployment primary key.</param>
    /// <param name="token">Optional form-access token; when present must own this deployment.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartDeployment(int id, string? token, CancellationToken cancellationToken)
    {
        bool isTokenStart = !string.IsNullOrWhiteSpace(token);
        if (isTokenStart)
        {
            FormAccessToken? access = await _formAccessTokenService.ResolveActiveAsync(token!);
            if (access is null || access.AppDeploymentId != id)
            {
                return View("InvalidToken");
            }
        }

        AppDeployment? deployment = await _service.GetByIdAsync(id);
        if (deployment is null)
        {
            return NotFound();
        }

        try
        {
            string jobId = await _workflowOrchestration.StartWorkflowJobFromDeploymentAsync(
                deployment,
                savedMessage: "App deployment process started. Triggering base workflow...",
                cancellationToken);

            return RedirectToAction(nameof(WorkflowProgress), new { jobId });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Could not start deployment workflow for {DeploymentId}", id);
            TempData["WarningMessage"] = ex.Message;

            if (isTokenStart)
            {
                return RedirectToAction(nameof(Form), new { token });
            }

            return RedirectToAction(nameof(Edit), new { id });
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
    /// Shows the edit form (admin path by id, or client path when <paramref name="token"/> is supplied).
    /// </summary>
    public async Task<IActionResult> Edit(int id, string? token = null)
    {
        // Client token path: only allow editing the deployment linked to that token.
        if (!string.IsNullOrWhiteSpace(token))
        {
            FormAccessToken? access = await _formAccessTokenService.ResolveActiveAsync(token);
            if (access is null || access.AppDeploymentId != id)
            {
                return View("InvalidToken");
            }

            ViewBag.FormToken = access.Token;
        }

        AppDeployment? deployment = await _service.GetByIdAsync(id);
        if (deployment == null)
        {
            return NotFound();
        }

        deployment.OneSignalRestApiKey = string.Empty;
        ViewBag.DeploymentId = deployment.Id;
        return View(deployment);
    }

    /// <summary>
    /// Updates an existing deployment. Does not start or re-trigger the base workflow.
    /// </summary>
    /// <remarks>
    /// When <paramref name="token"/> is present, the token must own <paramref name="id"/>.
    /// Admin edits from Index omit the token.
    /// </remarks>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> Edit(
        int id,
        string? token,
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

        bool isTokenEdit = !string.IsNullOrWhiteSpace(token);
        if (isTokenEdit)
        {
            FormAccessToken? access = await _formAccessTokenService.ResolveActiveAsync(token!);
            if (access is null || access.AppDeploymentId != id)
            {
                return View("InvalidToken");
            }

            ViewBag.FormToken = access.Token;
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
            ViewBag.DeploymentId = model.Id;
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
            ViewBag.DeploymentId = model.Id;
            return View(model);
        }

        TempData["SuccessMessage"] = "Deployment saved successfully.";

        // Client stays on the tokenized form link; admin stays on the edit form.
        if (isTokenEdit)
        {
            return RedirectToAction(nameof(Form), new { token });
        }

        return RedirectToAction(nameof(Edit), new { id });
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
    /// Caps prefilled App Name to the model's max length so validation does not fail on first render.
    /// </summary>
    private static string TruncateForAppName(string clientAppName)
    {
        const int maxLength = 30;
        string trimmed = clientAppName.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
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
