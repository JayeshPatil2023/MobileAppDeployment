using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using MobileAppDeployment.Models;
using MobileAppDeployment.Services;

namespace MobileAppDeployment.Controllers;

public class AppDeploymentController : Controller
{
    private static readonly string[] PngOnly = [".png"];
    private static readonly string[] PngOrJpeg = [".png", ".jpg", ".jpeg"];
    private static readonly string[] PlistOnly = [".plist"];
    private static readonly string[] JsonOnly = [".json"];

    private readonly IAppDeploymentService _service;
    private readonly IAssetStorageService _assetStorage;

    public AppDeploymentController(IAppDeploymentService service, IAssetStorageService assetStorage)
    {
        _service = service;
        _assetStorage = assetStorage;
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
        IFormFile? websiteLogoFile,
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
            websiteLogoFile,
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
            await SaveUploadedFilesAsync(id, model, websiteLogoFile, mobileAppIconFile, launchImageFile, storeIconFile, featureGraphicFile, firebaseIosConfigFile, firebaseAndroidConfigFile);
            await _service.UpdateAsync(model);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        TempData["SuccessMessage"] = "App deployment details saved successfully.";
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
        IFormFile? websiteLogoFile,
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
            await SaveUploadedFilesAsync(id, model, websiteLogoFile, mobileAppIconFile, launchImageFile, storeIconFile, featureGraphicFile, firebaseIosConfigFile, firebaseAndroidConfigFile);
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
        IFormFile? websiteLogoFile,
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

        if (websiteLogoFile is null || websiteLogoFile.Length == 0)
        {
            modelState.AddModelError("websiteLogoFile", "Website logo is required.");
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
        model.WebsiteLogoPath ??= existing.WebsiteLogoPath;
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
        IFormFile? websiteLogoFile,
        IFormFile? mobileAppIconFile,
        IFormFile? launchImageFile,
        IFormFile? storeIconFile,
        IFormFile? featureGraphicFile,
        IFormFile? firebaseIosConfigFile,
        IFormFile? firebaseAndroidConfigFile)
    {
        if (websiteLogoFile is { Length: > 0 })
        {
            model.WebsiteLogoPath = await _assetStorage.SaveAssetAsync(deploymentId, websiteLogoFile, "website-logo", PngOnly);
        }

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
