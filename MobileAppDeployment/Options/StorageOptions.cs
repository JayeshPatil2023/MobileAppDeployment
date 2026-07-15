namespace MobileAppDeployment.Options;

/// <summary>
/// Selects the active asset storage backend implementation.
/// </summary>
public class StorageOptions
{
    /// <summary>Configuration section name in appsettings.</summary>
    public const string SectionName = "Storage";

    /// <summary>
    /// Storage backend type. Default is <c>Local</c> (filesystem under wwwroot/uploads).
    /// Future backends (Azure Blob, S3) implement <c>IAssetStorageService</c> and are selected here.
    /// </summary>
    public string StorageType { get; set; } = "Local";
}
