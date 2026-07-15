using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MobileAppDeployment.Application.Validation;

/// <summary>
/// Applies the draft/save validation rule set for app deployments (Organization Name + App Name).
/// </summary>
/// <remarks>
/// Thin facade over <see cref="AppDeploymentValidation.ApplySaveValidation"/> for the Part 2.2 file layout.
/// </remarks>
public static class AppDeploymentSaveValidator
{
    /// <summary>
    /// Clears ModelState and re-validates only the save-required properties.
    /// </summary>
    public static void Apply(ModelStateDictionary modelState, AppDeployment model) =>
        AppDeploymentValidation.ApplySaveValidation(modelState, model);
}
