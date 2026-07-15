using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MobileAppDeployment.Application.AssetUpload;
using MobileAppDeployment.Application.Validation;
using MobileAppDeployment.Core.Domain.Entities;
using MobileAppDeployment.Core.Interfaces.Services;
using MobileAppDeployment.Core.Models.ViewModels;
using MobileAppDeployment.Extensions;

namespace MobileAppDeployment.Web.Controllers;

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
/// </remarks>
public class AppDeploymentController : Controller
{
    private readonly IAppDeploymentService _service;
    private readonly IWorkflowOrchestrationService _workflowOrchestration;
    private readonly IFormAccessTokenService _formAccessTokenService;
    private readonly ILogger<AppDeploymentController> _logger;

    /// <summary>
    /// Creates a new controller instance.
    /// </summary>
    public AppDeploymentController(
        IAppDeploymentService service,
        IWorkflowOrchestrationService workflowOrchestration,
        IFormAccessTokenService formAccessTokenService,
        ILogger<AppDeploymentController> logger)
    {
        _service = service;
        _workflowOrchestration = workflowOrchestration;
        _formAccessTokenService = formAccessTokenService;
        _logger = logger;
    }

    /// <summary>
    /// Lists saved deployments (admin/internal view).
    /// </summary>
    [Authorize]
    public async Task<IActionResult> Index() =>
        View(await _service.GetListItemsAsync());

    /// <summary>
    /// Entry point for a client magic-link: shows Create if the token is unused, otherwise Edit.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [Route("AppDeployment/Form/{token}")]
    public async Task<IActionResult> Form(string token)
    {
        FormAccessToken? access = await _formAccessTokenService.ResolveActiveAsync(token);
        if (access is null)
        {
            return View("InvalidToken");
        }

        if (access.AppDeploymentId is int deploymentId)
        {
            AppDeployment? deployment = await _service.GetByIdAsync(deploymentId);
            if (deployment is null)
            {
                _logger.LogWarning(
                    "Form token {TokenId} points at missing deployment {DeploymentId}.",
                    access.Id,
                    deploymentId);
                return View("InvalidToken");
            }

            BindFormContext(access.Token, deployment.Id);
            return View("Edit", AppDeploymentFormViewModel.FromEntity(deployment));
        }

        BindFormContext(access.Token, deploymentId: null);
        return View("Create", AppDeploymentFormViewModel.ForNewDeployment(access.ClientAppName));
    }

    /// <summary>
    /// Open Create without a token is disabled — clients must use the magic link.
    /// </summary>
    public IActionResult Create() => View("InvalidToken");

    /// <summary>
    /// Saves deployment details for a valid unused token. Does not start the GitHub Actions workflow.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(52_428_800)]
    [EnableRateLimiting(RateLimitingExtensions.FormSubmitPolicy)]
    public async Task<IActionResult> Create(
        string token,
        AppDeploymentFormViewModel model,
        IFormFile? mobileAppIconFile,
        IFormFile? launchImageFile,
        IFormFile? storeIconFile,
        IFormFile? featureGraphicFile,
        IFormFile? firebaseIosConfigFile,
        IFormFile? firebaseAndroidConfigFile,
        IFormFile? playStoreKeyFile,
        IFormFile? appleAuthKeyFile)
    {
        FormAccessToken? access = await _formAccessTokenService.ResolveActiveAsync(token);
        if (access is null)
        {
            return View("InvalidToken");
        }

        if (access.AppDeploymentId.HasValue)
        {
            return RedirectToAction(nameof(Form), new { token = access.Token });
        }

        BindFormContext(access.Token, deploymentId: null);
        return await SaveDraftAsync(
            model,
            existing: null,
            access.Token,
            isCreate: true,
            () => RedirectToAction(nameof(Form), new { token = access.Token }),
            mobileAppIconFile, launchImageFile, storeIconFile, featureGraphicFile,
            firebaseIosConfigFile, firebaseAndroidConfigFile, playStoreKeyFile, appleAuthKeyFile);
    }

    /// <summary>
    /// Explicitly starts the base GitHub Actions workflow for a saved deployment.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartDeployment(int id, string? token, CancellationToken cancellationToken)
    {
        bool isTokenStart = !string.IsNullOrWhiteSpace(token);
        if (isTokenStart)
        {
            FormAccessToken? access = await ResolveOwnedTokenAsync(token!, id);
            if (access is null)
            {
                return View("InvalidToken");
            }

            ViewBag.FormToken = access.Token;
        }

        AppDeployment? deployment = await _service.GetByIdAsync(id);
        if (deployment is null)
        {
            return NotFound();
        }

        if (!AppDeploymentValidation.ValidateForDeployment(deployment, ModelState))
        {
            _logger.LogInformation(
                "StartDeployment blocked for {DeploymentId}: form is incomplete ({ErrorCount} validation errors).",
                id,
                ModelState.ErrorCount);
            TempData["WarningMessage"] =
                "Cannot start deployment until all required fields and assets are completed. See the errors below.";
            ViewBag.DeploymentId = deployment.Id;
            return View("Edit", AppDeploymentFormViewModel.FromEntity(deployment));
        }

        try
        {
            string jobId = await _workflowOrchestration.StartWorkflowJobFromDeploymentAsync(
                deployment,
                savedMessage: "App deployment process started. Triggering base workflow...",
                cancellationToken);
            return RedirectToAction("WorkflowProgress", "Workflow", new { jobId });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Could not start deployment workflow for {DeploymentId}", id);
            TempData["WarningMessage"] = ex.Message;
            return isTokenStart
                ? RedirectToAction(nameof(Form), new { token })
                : RedirectToAction(nameof(Edit), new { id });
        }
    }

    /// <summary>
    /// Shows a single deployment.
    /// </summary>
    [Authorize]
    public async Task<IActionResult> Details(int id)
    {
        AppDeployment? deployment = await _service.GetByIdAsync(id);
        return deployment is null ? NotFound() : View(AppDeploymentDetailsViewModel.FromEntity(deployment));
    }

    /// <summary>
    /// Shows the edit form (admin path by id, or client path when <paramref name="token"/> is supplied).
    /// </summary>
    public async Task<IActionResult> Edit(int id, string? token = null)
    {
        if (string.IsNullOrWhiteSpace(token) && !(User.Identity?.IsAuthenticated ?? false))
        {
            return Challenge();
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            FormAccessToken? access = await ResolveOwnedTokenAsync(token, id);
            if (access is null)
            {
                return View("InvalidToken");
            }

            ViewBag.FormToken = access.Token;
        }

        AppDeployment? deployment = await _service.GetByIdAsync(id);
        if (deployment is null)
        {
            return NotFound();
        }

        ViewBag.DeploymentId = deployment.Id;
        return View(AppDeploymentFormViewModel.FromEntity(deployment));
    }

    /// <summary>
    /// Updates an existing deployment. Does not start or re-trigger the base workflow.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(52_428_800)]
    [EnableRateLimiting(RateLimitingExtensions.FormSubmitPolicy)]
    public async Task<IActionResult> Edit(
        int id,
        string? token,
        AppDeploymentFormViewModel model,
        IFormFile? mobileAppIconFile,
        IFormFile? launchImageFile,
        IFormFile? storeIconFile,
        IFormFile? featureGraphicFile,
        IFormFile? firebaseIosConfigFile,
        IFormFile? firebaseAndroidConfigFile,
        IFormFile? playStoreKeyFile,
        IFormFile? appleAuthKeyFile)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        bool isTokenEdit = !string.IsNullOrWhiteSpace(token);
        if (!isTokenEdit && !(User.Identity?.IsAuthenticated ?? false))
        {
            return Challenge();
        }

        if (isTokenEdit)
        {
            FormAccessToken? access = await ResolveOwnedTokenAsync(token!, id);
            if (access is null)
            {
                return View("InvalidToken");
            }

            ViewBag.FormToken = access.Token;
        }

        AppDeployment? existing = await _service.GetByIdAsync(id);
        if (existing is null)
        {
            return NotFound();
        }

        return await SaveDraftAsync(
            model,
            existing,
            token,
            isCreate: false,
            () => isTokenEdit
                ? RedirectToAction(nameof(Form), new { token })
                : RedirectToAction(nameof(Edit), new { id }),
            mobileAppIconFile, launchImageFile, storeIconFile, featureGraphicFile,
            firebaseIosConfigFile, firebaseAndroidConfigFile, playStoreKeyFile, appleAuthKeyFile);
    }

    /// <summary>
    /// Confirms deletion.
    /// </summary>
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        AppDeployment? deployment = await _service.GetByIdAsync(id);
        return deployment is null ? NotFound() : View(AppDeploymentDetailsViewModel.FromEntity(deployment));
    }

    /// <summary>
    /// Deletes a deployment and its stored assets.
    /// </summary>
    [HttpPost, ActionName("Delete")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (!await _service.DeleteAsync(id))
        {
            return NotFound();
        }

        TempData["SuccessMessage"] = "App deployment deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> SaveDraftAsync(
        AppDeploymentFormViewModel model,
        AppDeployment? existing,
        string? token,
        bool isCreate,
        Func<IActionResult> onSuccessRedirect,
        IFormFile? mobileAppIconFile,
        IFormFile? launchImageFile,
        IFormFile? storeIconFile,
        IFormFile? featureGraphicFile,
        IFormFile? firebaseIosConfigFile,
        IFormFile? firebaseAndroidConfigFile,
        IFormFile? playStoreKeyFile,
        IFormFile? appleAuthKeyFile)
    {
        if (existing is not null && string.IsNullOrWhiteSpace(model.OneSignalRestApiKey))
        {
            ModelState.Remove(nameof(AppDeployment.OneSignalRestApiKey));
        }

        AppDeployment entity = model.MapToEntity(existing);
        AppDeploymentValidation.ApplySaveValidation(ModelState, entity);
        AssetImageValidator.ValidateUploadedImages(
            ModelState, mobileAppIconFile, launchImageFile, storeIconFile, featureGraphicFile);

        if (!ModelState.IsValid)
        {
            ViewBag.DeploymentId = model.Id == 0 ? (object?)null : model.Id;
            return View(isCreate ? "Create" : "Edit", model);
        }

        try
        {
            bool saved = await _service.SaveDraftWithAssetsAsync(
                entity,
                id => AssetUploadCommand.FromFormFiles(
                    id, mobileAppIconFile, launchImageFile, storeIconFile, featureGraphicFile,
                    firebaseIosConfigFile, firebaseAndroidConfigFile, playStoreKeyFile, appleAuthKeyFile),
                token,
                isCreate);

            if (!saved)
            {
                return NotFound();
            }

            TempData["SuccessMessage"] =
                "Deployment saved successfully. Complete all required fields before starting deployment.";
            return onSuccessRedirect();
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewBag.DeploymentId = model.Id == 0 ? (object?)null : model.Id;
            return View(isCreate ? "Create" : "Edit", model);
        }
    }

    private async Task<FormAccessToken?> ResolveOwnedTokenAsync(string token, int deploymentId)
    {
        FormAccessToken? access = await _formAccessTokenService.ResolveActiveAsync(token);
        return access is not null && access.AppDeploymentId == deploymentId ? access : null;
    }

    private void BindFormContext(string token, int? deploymentId)
    {
        ViewBag.FormToken = token;
        ViewBag.DeploymentId = deploymentId;
    }
}
