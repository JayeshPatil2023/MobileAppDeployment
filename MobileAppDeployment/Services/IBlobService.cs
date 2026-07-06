namespace MobileAppDeployment.Services;

public interface IBlobService
{
    Task<string> UploadFileAsync(IFormFile file, string containerName, string fileName, string? folderName = null, CancellationToken cancellationToken = default);

    Task DeleteFileAsync(string fileName, string containerName, string? folderName = null, CancellationToken cancellationToken = default);

    Task DeleteFolderAsync(string containerName, string folderName, CancellationToken cancellationToken = default);

    Task<byte[]> DownloadBlobAsync(string filePath, string containerName);
}
