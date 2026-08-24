using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace eTicketing.Shared.Storage;

public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _client;

    public AzureBlobStorageService(IOptions<BlobStorageOptions> options)
    {
        _client = new BlobServiceClient(options.Value.ConnectionString);
    }

    public async Task<string> UploadAsync(string containerName, string blobName, Stream content, string contentType, CancellationToken ct = default)
    {
        var container = _client.GetBlobContainerClient(containerName);
        // Lazy + idempotent: cheap to call on every upload, self-heals if the container was ever
        // deleted out-of-band. PublicAccessType.Blob = anonymous read of blobs only, no container
        // listing — the account this connects to has "Allow Blob public access" enabled.
        await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: ct);

        var blob = container.GetBlobClient(blobName);
        await blob.UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
            ct);

        return blob.Uri.ToString();
    }

    public async Task<byte[]?> DownloadAsync(string containerName, string blobName, CancellationToken ct = default)
    {
        var blob = _client.GetBlobContainerClient(containerName).GetBlobClient(blobName);

        // ExistsAsync first rather than catching RequestFailedException: a missing PDF is an
        // expected outcome (the blob can be deleted out-of-band), not an exceptional one.
        if (!await blob.ExistsAsync(ct))
        {
            return null;
        }

        using var buffer = new MemoryStream();
        await blob.DownloadToAsync(buffer, ct);
        return buffer.ToArray();
    }

    public async Task DeleteAsync(string containerName, string blobName, CancellationToken ct = default)
    {
        var container = _client.GetBlobContainerClient(containerName);
        await container.DeleteBlobIfExistsAsync(blobName, cancellationToken: ct);
    }

    public string GetPublicUrl(string containerName, string blobName)
        => _client.GetBlobContainerClient(containerName).GetBlobClient(blobName).Uri.ToString();
}
