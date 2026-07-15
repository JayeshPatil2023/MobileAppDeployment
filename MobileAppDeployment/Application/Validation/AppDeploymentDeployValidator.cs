using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MobileAppDeployment.Application.Validation;

/// <summary>
/// Applies full deployment validation before starting the GitHub workflow process.
/// </summary>
/// <remarks>
/// Thin facade over <see cref="AppDeploymentValidation.ValidateForDeployment"/> for the Part 2.2 file layout.
/// </remarks>
public static class AppDeploymentDeployValidator
{
    /// <summary>
    /// Runs full annotation + custom validation; returns <c>true</c> when the model is ready to deploy.
    /// </summary>
    public static bool Validate(AppDeployment model, ModelStateDictionary modelState) =>
        AppDeploymentValidation.ValidateForDeployment(model, modelState);
}
