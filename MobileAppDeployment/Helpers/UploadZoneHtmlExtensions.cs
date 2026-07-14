using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MobileAppDeployment.Helpers;

/// <summary>
/// UI helpers for file upload zones that mirror text-input validation styling.
/// </summary>
/// <remarks>
/// Custom upload cards are plain <c>div</c>s, so ASP.NET never adds
/// <c>input-validation-error</c>. These helpers apply <c>upload-zone--invalid</c>
/// and render ModelState messages for file field keys (e.g. <c>playStoreKeyFile</c>).
/// </remarks>
public static class UploadZoneHtmlExtensions
{
    /// <summary>
    /// Returns true when <paramref name="fieldName"/> has at least one ModelState error.
    /// </summary>
    public static bool HasUploadError(this IHtmlHelper html, string fieldName)
    {
        ModelStateEntry? entry = html.ViewData.ModelState[fieldName];
        return entry is { Errors.Count: > 0 };
    }

    /// <summary>
    /// Builds CSS classes for an upload zone, including invalid state from ModelState.
    /// </summary>
    /// <param name="html">View HTML helper.</param>
    /// <param name="fieldName">Form file input name / ModelState key.</param>
    /// <param name="hasExistingFile">True when a stored file path already exists.</param>
    /// <param name="extraClasses">Optional extra classes (e.g. <c>config-upload-zone</c>).</param>
    public static string UploadZoneClasses(
        this IHtmlHelper html,
        string fieldName,
        bool hasExistingFile,
        string? extraClasses = null)
    {
        var parts = new List<string> { "upload-zone" };

        if (!string.IsNullOrWhiteSpace(extraClasses))
        {
            parts.Add(extraClasses.Trim());
        }

        if (hasExistingFile)
        {
            parts.Add("has-file");
        }

        // Server-side StartDeployment validation paints the card red like .input-validation-error.
        if (html.HasUploadError(fieldName))
        {
            parts.Add("upload-zone--invalid");
        }

        return string.Join(' ', parts);
    }
}
