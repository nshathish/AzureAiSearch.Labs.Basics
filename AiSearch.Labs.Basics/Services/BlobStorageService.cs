using AiSearch.Labs.Basics.Configuration;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;
using System.Text;

namespace AiSearch.Labs.Basics.Services;

public sealed class BlobStorageService
{
    private readonly BlobContainerClient _containerClient;

    public BlobStorageService(IOptions<AzureStorageOptions> azureStorageOptions)
    {
        var connectionString = azureStorageOptions.Value.ConnectionString;
        var containerName = azureStorageOptions.Value.ContainerName;

        _containerClient = new BlobContainerClient(connectionString, containerName);
    }

    public async Task<string> UploadAsync(
        IBrowserFile file,
        long maxAllowedSize,
        CancellationToken cancellationToken = default)
    {
        await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobName = CreateSafeBlobName(file.Name);
        var blobClient = _containerClient.GetBlobClient(blobName);

        await using var stream = file.OpenReadStream(maxAllowedSize, cancellationToken);
        await blobClient.UploadAsync(stream, overwrite: false, cancellationToken);

        return blobName;
    }

    public async Task<List<string>> GetDocumentsAsync()
    {
        var documents = new List<string>();

        await foreach (var blob in _containerClient.GetBlobsAsync())
        {
            documents.Add(blob.Name);
        }

        return documents;
    }

    private static string CreateSafeBlobName(string originalName)
    {
        var fileName = Path.GetFileName(originalName);
        var extension = Path.GetExtension(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);

        var cleanStem = new StringBuilder();
        foreach (var c in stem)
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_')
                cleanStem.Append(c);
            else if (char.IsWhiteSpace(c))
                cleanStem.Append('-');
        }

        var readableName = cleanStem.Length == 0 ? "document" : cleanStem.ToString();
        if (readableName.Length > 80)
            readableName = readableName[..80];

        return $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}-{readableName}{extension}";
    }
}