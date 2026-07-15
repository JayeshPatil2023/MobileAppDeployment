namespace MobileAppDeployment.Application.Validation;

/// <summary>
/// Declares accepted type, size, and pixel dimensions for each image asset upload.
/// </summary>
/// <remarks>
/// Specs drive both server-side validation and the data-* attributes used by client-side checks.
/// Leave <see cref="MaxBytes"/> null when no explicit size cap is required.
/// </remarks>
public sealed class AssetUploadSpec
{
    /// <summary>
    /// Form file input name / ModelState key (e.g. <c>mobileAppIconFile</c>).
    /// </summary>
    public required string FieldName { get; init; }

    /// <summary>
    /// Friendly label used in error messages.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Allowed file extensions including the leading dot (lowercase).
    /// </summary>
    public required string[] AllowedExtensions { get; init; }

    /// <summary>
    /// Required image width in pixels.
    /// </summary>
    public required int Width { get; init; }

    /// <summary>
    /// Required image height in pixels.
    /// </summary>
    public required int Height { get; init; }

    /// <summary>
    /// Optional maximum file size in bytes.
    /// </summary>
    public long? MaxBytes { get; init; }

    /// <summary>
    /// Catalog of image asset rules used by Create/Edit upload validation.
    /// </summary>
    public static IReadOnlyDictionary<string, AssetUploadSpec> All { get; } =
        new Dictionary<string, AssetUploadSpec>(StringComparer.OrdinalIgnoreCase)
        {
            // App icon: PNG or JPEG, ≤ 1 MB, exactly 512 × 512.
            ["mobileAppIconFile"] = new AssetUploadSpec
            {
                FieldName = "mobileAppIconFile",
                DisplayName = "Mobile app icon",
                AllowedExtensions = [".png", ".jpg", ".jpeg"],
                Width = 512,
                Height = 512,
                MaxBytes = 1 * 1024 * 1024
            },
            // Launch image — keep existing PNG 2732 × 2732 requirement.
            ["launchImageFile"] = new AssetUploadSpec
            {
                FieldName = "launchImageFile",
                DisplayName = "Launch image",
                AllowedExtensions = [".png"],
                Width = 2732,
                Height = 2732,
                MaxBytes = null
            },
            // Store icon — keep existing PNG 512 × 512 requirement.
            ["storeIconFile"] = new AssetUploadSpec
            {
                FieldName = "storeIconFile",
                DisplayName = "Store icon",
                AllowedExtensions = [".png"],
                Width = 512,
                Height = 512,
                MaxBytes = null
            },
            // Feature graphic: PNG or JPEG, ≤ 15 MB, exactly 1024 × 500.
            ["featureGraphicFile"] = new AssetUploadSpec
            {
                FieldName = "featureGraphicFile",
                DisplayName = "Feature graphic",
                AllowedExtensions = [".png", ".jpg", ".jpeg"],
                Width = 1024,
                Height = 500,
                MaxBytes = 15L * 1024 * 1024
            }
        };

    /// <summary>
    /// Looks up a known asset rule by form field name.
    /// </summary>
    public static bool TryGet(string fieldName, out AssetUploadSpec spec) =>
        All.TryGetValue(fieldName, out spec!);
}
