using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using MobileAppDeployment.Models;

namespace MobileAppDeployment.Helpers;

/// <summary>
/// Validation rules that differ between <strong>Save</strong> and <strong>Start Deployment</strong>.
/// </summary>
/// <remarks>
/// <para>
/// Save (Create/Edit) only requires Organization Name and App Name so clients can
/// persist partial drafts. All other <see cref="RequiredAttribute"/> markers stay on the
/// model for UI asterisks and for full validation at deployment time.
/// </para>
/// <para>
/// Start Deployment validates every required property and required asset path before
/// the GitHub workflow is allowed to run.
/// </para>
/// </remarks>
public static class AppDeploymentValidation
{
    /// <summary>
    /// Property names that must be present when saving a draft (Create or Edit).
    /// </summary>
    public static readonly string[] SaveRequiredPropertyNames =
    [
        nameof(AppDeployment.OrganizationName),
        nameof(AppDeployment.AppName)
    ];

    /// <summary>
    /// Form file input names that are required before starting deployment.
    /// </summary>
    private static readonly (string ModelStateKey, string PathProperty, string ErrorMessage)[] RequiredAssets =
    [
        ("mobileAppIconFile", nameof(AppDeployment.MobileAppIconPath), "Mobile app icon is required."),
        ("launchImageFile", nameof(AppDeployment.LaunchImagePath), "Launch image is required."),
        ("storeIconFile", nameof(AppDeployment.StoreIconPath), "Store icon is required."),
        ("featureGraphicFile", nameof(AppDeployment.FeatureGraphicPath), "Feature graphic is required."),
        ("firebaseIosConfigFile", nameof(AppDeployment.FirebaseIosConfigPath), "GoogleService-Info.plist is required."),
        ("firebaseAndroidConfigFile", nameof(AppDeployment.FirebaseAndroidConfigPath), "google-services.json is required."),
        ("playStoreKeyFile", nameof(AppDeployment.PlayStoreKeyPath), "Play Store key JSON file is required."),
        ("appleAuthKeyFile", nameof(AppDeployment.AppleAuthKeyPath), "Apple auth key (.p8) file is required.")
    ];

    /// <summary>
    /// Relaxes automatic model binding validation so Create/Edit can save partial forms.
    /// </summary>
    /// <remarks>
    /// Keeps errors only for <see cref="SaveRequiredPropertyNames"/>, then ensures those
    /// two fields are present. Format/length errors on other fields are cleared so empty
    /// optional-at-save fields (Country min length, Team ID min length, etc.) do not block save.
    /// Also coerces null non-nullable strings to empty so PostgreSQL NOT NULL columns accept drafts.
    /// </remarks>
    /// <param name="modelState">Controller <see cref="ModelStateDictionary"/>.</param>
    /// <param name="model">Posted deployment model.</param>
    public static void ApplySaveValidation(ModelStateDictionary modelState, AppDeployment model)
    {
        // Model binding leaves omitted/empty inputs as null; DB columns are still NOT NULL.
        NormalizeNonNullableStringsForPartialSave(model);

        // Snapshot keys first — ModelState.Keys mutates when entries are removed.
        foreach (string key in modelState.Keys.ToList())
        {
            bool isSaveRequired = SaveRequiredPropertyNames.Any(p =>
                string.Equals(key, p, StringComparison.OrdinalIgnoreCase));

            if (!isSaveRequired)
            {
                modelState.Remove(key);
            }
        }

        // Re-assert the two save-required fields even if binding left them as empty with no error.
        if (string.IsNullOrWhiteSpace(model.OrganizationName))
        {
            modelState.Remove(nameof(AppDeployment.OrganizationName));
            modelState.AddModelError(
                nameof(AppDeployment.OrganizationName),
                "Organization Name is required.");
        }

        if (string.IsNullOrWhiteSpace(model.AppName))
        {
            modelState.Remove(nameof(AppDeployment.AppName));
            modelState.AddModelError(
                nameof(AppDeployment.AppName),
                "App Name is required.");
        }
        else if (model.AppName.Length > 30)
        {
            // Keep max-length guard for App Name even though other fields are relaxed.
            modelState.AddModelError(
                nameof(AppDeployment.AppName),
                "App Name must be 30 characters or less.");
        }
    }

    /// <summary>
    /// Replaces null on non-nullable string properties with <see cref="string.Empty"/>.
    /// </summary>
    /// <remarks>
    /// ASP.NET model binding sets missing form fields to <c>null</c> even when the CLR
    /// property type is non-nullable <c>string</c>. PostgreSQL then rejects the insert/update
    /// with error 23502 (NOT NULL). Empty string satisfies NOT NULL while still failing
    /// <see cref="ValidateForDeployment"/> later via <see cref="RequiredAttribute"/>.
    /// Nullable path/URL properties are left unchanged.
    /// </remarks>
    /// <param name="model">Deployment being saved as a partial draft.</param>
    public static void NormalizeNonNullableStringsForPartialSave(AppDeployment model)
    {
        model.CommonName ??= string.Empty;
        model.OrganizationUnit ??= string.Empty;
        model.OrganizationName ??= string.Empty;
        model.LocalityName ??= string.Empty;
        model.StateName ??= string.Empty;
        // Keep the form default when Country was never posted (null), but allow "" if cleared.
        model.Country ??= "US";
        model.AdminEmail ??= string.Empty;

        model.AppName ??= string.Empty;
        model.ShortDescription ??= string.Empty;
        model.FullDescription ??= string.Empty;

        model.AppleName ??= string.Empty;
        model.AppleSubtitle ??= string.Empty;
        model.AppleDescription ??= string.Empty;
        model.AppleKeywords ??= string.Empty;
        model.AppleCopyright ??= string.Empty;

        model.ContactFirstName ??= string.Empty;
        model.ContactLastName ??= string.Empty;
        model.ContactPhoneNumber ??= string.Empty;
        model.ContactEmailAddress ??= string.Empty;
        model.PrivacyPolicyUrl ??= string.Empty;

        model.AndroidPackageName ??= string.Empty;
        model.IosBundleId ??= string.Empty;
        model.AppleTeamId ??= string.Empty;
        model.AppleIssuerId ??= string.Empty;
        model.AppleKeyId ??= string.Empty;

        model.DomainUrl ??= string.Empty;

        model.OneSignalSenderId ??= string.Empty;
        model.OneSignalAppId ??= string.Empty;
        model.OneSignalRestApiKey ??= string.Empty;
    }

    /// <summary>
    /// Runs full data-annotation validation plus required asset path checks for Start Deployment.
    /// </summary>
    /// <param name="model">Persisted deployment loaded from the database.</param>
    /// <param name="modelState">ModelState to populate with field-level errors.</param>
    /// <returns><c>true</c> when the deployment is complete enough to start the workflow.</returns>
    public static bool ValidateForDeployment(AppDeployment model, ModelStateDictionary modelState)
    {
        modelState.Clear();

        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();

        // validateAllProperties: true runs Required, StringLength, EmailAddress, Url, etc.
        _ = Validator.TryValidateObject(model, context, results, validateAllProperties: true);

        foreach (ValidationResult result in results)
        {
            string key = result.MemberNames.FirstOrDefault() ?? string.Empty;
            modelState.AddModelError(key, result.ErrorMessage ?? "Invalid value.");
        }

        ValidateRequiredAssetPaths(model, modelState);

        return modelState.IsValid;
    }

    /// <summary>
    /// Ensures every required upload has a stored path on the deployment row.
    /// </summary>
    private static void ValidateRequiredAssetPaths(AppDeployment model, ModelStateDictionary modelState)
    {
        foreach ((string modelStateKey, string pathProperty, string errorMessage) in RequiredAssets)
        {
            string? path = pathProperty switch
            {
                nameof(AppDeployment.MobileAppIconPath) => model.MobileAppIconPath,
                nameof(AppDeployment.LaunchImagePath) => model.LaunchImagePath,
                nameof(AppDeployment.StoreIconPath) => model.StoreIconPath,
                nameof(AppDeployment.FeatureGraphicPath) => model.FeatureGraphicPath,
                nameof(AppDeployment.FirebaseIosConfigPath) => model.FirebaseIosConfigPath,
                nameof(AppDeployment.FirebaseAndroidConfigPath) => model.FirebaseAndroidConfigPath,
                nameof(AppDeployment.PlayStoreKeyPath) => model.PlayStoreKeyPath,
                nameof(AppDeployment.AppleAuthKeyPath) => model.AppleAuthKeyPath,
                _ => null
            };

            if (string.IsNullOrWhiteSpace(path))
            {
                modelState.AddModelError(modelStateKey, errorMessage);
            }
        }
    }
}
