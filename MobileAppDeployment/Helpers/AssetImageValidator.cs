using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MobileAppDeployment.Helpers;

/// <summary>
/// Validates uploaded image assets against <see cref="AssetUploadSpec"/> rules
/// (extension, optional max size, and exact pixel dimensions).
/// </summary>
public static class AssetImageValidator
{
    /// <summary>
    /// Validates each provided image upload and adds field errors to <paramref name="modelState"/>.
    /// </summary>
    /// <remarks>
    /// Empty / missing files are skipped — required-file checks happen at Start Deployment.
    /// When a file <em>is</em> posted, it must satisfy the full type/size/dimension rules.
    /// </remarks>
    public static void ValidateUploadedImages(
        ModelStateDictionary modelState,
        IFormFile? mobileAppIconFile,
        IFormFile? launchImageFile,
        IFormFile? storeIconFile,
        IFormFile? featureGraphicFile)
    {
        ValidateOne(modelState, "mobileAppIconFile", mobileAppIconFile);
        ValidateOne(modelState, "launchImageFile", launchImageFile);
        ValidateOne(modelState, "storeIconFile", storeIconFile);
        ValidateOne(modelState, "featureGraphicFile", featureGraphicFile);
    }

    /// <summary>
    /// Validates a single uploaded file against its catalogued asset rule.
    /// </summary>
    private static void ValidateOne(ModelStateDictionary modelState, string fieldName, IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return;
        }

        if (!AssetUploadSpec.TryGet(fieldName, out AssetUploadSpec spec))
        {
            return;
        }

        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!spec.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            modelState.AddModelError(
                fieldName,
                $"{spec.DisplayName} must be {FormatExtensions(spec.AllowedExtensions)}.");
            return;
        }

        if (spec.MaxBytes is long maxBytes && file.Length > maxBytes)
        {
            modelState.AddModelError(
                fieldName,
                $"{spec.DisplayName} must be {FormatByteSize(maxBytes)} or smaller.");
            return;
        }

        try
        {
            using Stream stream = file.OpenReadStream();
            if (!ImageHeaderReader.TryGetDimensions(stream, out int width, out int height))
            {
                modelState.AddModelError(
                    fieldName,
                    $"{spec.DisplayName} could not be read as a valid image. Upload a valid PNG or JPEG.");
                return;
            }

            if (width != spec.Width || height != spec.Height)
            {
                modelState.AddModelError(
                    fieldName,
                    $"{spec.DisplayName} must be exactly {spec.Width} × {spec.Height} px (uploaded image is {width} × {height} px).");
            }
        }
        catch
        {
            modelState.AddModelError(
                fieldName,
                $"{spec.DisplayName} could not be validated. Try a different PNG or JPEG file.");
        }
    }

    private static string FormatExtensions(IEnumerable<string> extensions)
    {
        string[] labels = extensions
            .Select(e => e.TrimStart('.').ToUpperInvariant())
            .Distinct()
            .ToArray();

        return labels.Length switch
        {
            0 => "an allowed image type",
            1 => labels[0],
            2 => $"{labels[0]} or {labels[1]}",
            _ => string.Join(", ", labels.Take(labels.Length - 1)) + $", or {labels[^1]}"
        };
    }

    private static string FormatByteSize(long bytes)
    {
        if (bytes >= 1024 * 1024)
        {
            double mb = bytes / (1024d * 1024d);
            return mb % 1 == 0 ? $"{(int)mb} MB" : $"{mb:0.#} MB";
        }

        double kb = bytes / 1024d;
        return kb % 1 == 0 ? $"{(int)kb} KB" : $"{kb:0.#} KB";
    }
}
