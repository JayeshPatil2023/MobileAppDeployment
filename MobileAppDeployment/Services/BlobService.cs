using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace MobileAppDeployment.Services;

public class BlobService : IBlobService
{
    private readonly BlobServiceClient _blobServiceClient;

    public BlobService(IConfiguration configuration)
    {
        string? connectionString = configuration.GetSection("AzureBlobStorage:ConnectionString").Value;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("AzureBlobStorage:ConnectionString is not configured.");
        }

        _blobServiceClient = new BlobServiceClient(connectionString);
    }

    public async Task<string> UploadFileAsync(
        IFormFile file,
        string containerName,
        string fileName,
        string? folderName = null,
        CancellationToken cancellationToken = default)
    {
        BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        string blobPath = string.IsNullOrEmpty(folderName) ? fileName : $"{folderName}/{fileName}";
        BlobClient blobClient = containerClient.GetBlobClient(blobPath);

        await using Stream targetStream = file.OpenReadStream();
        await blobClient.UploadAsync(targetStream, overwrite: true, cancellationToken);

        return blobClient.Uri.ToString();
    }

    public async Task DeleteFileAsync(
        string fileName,
        string containerName,
        string? folderName = null,
        CancellationToken cancellationToken = default)
    {
        BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        string blobPath = string.IsNullOrEmpty(folderName) ? fileName : $"{folderName}/{fileName}";
        await containerClient.DeleteBlobIfExistsAsync(blobPath, cancellationToken: cancellationToken);
    }

    public async Task DeleteFolderAsync(
        string containerName,
        string folderName,
        CancellationToken cancellationToken = default)
    {
        BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        if (!await containerClient.ExistsAsync(cancellationToken))
        {
            return;
        }

        string prefix = $"{folderName.TrimEnd('/')}/";
        await foreach (var blobItem in containerClient.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix: prefix, cancellationToken: cancellationToken))
        {
            await containerClient.DeleteBlobIfExistsAsync(blobItem.Name, cancellationToken: cancellationToken);
        }
    }

    public Task<byte[]> DownloadBlobAsync(string filePath, string containerName)
    {
        throw new NotImplementedException();
    }
}
