using eTicketing.Shared.Storage;

namespace eTicketing.PdfGeneration.Tests.TestSupport;

/// <summary>
/// In-memory IBlobStorageService test double — Azure Blob Storage is a genuine external system per
/// the testing conventions (mock it, don't hit real Azure from a unit test), but a real
/// implementation lets tests assert what was actually uploaded (a real PDF, under the expected
/// blob name) rather than just that a call was made.
/// </summary>
public sealed class FakeBlobStorageService : IBlobStorageService
{
    private readonly Dictionary<string, byte[]> _blobs = [];

    /// <summary>When set, UploadAsync throws — lets tests simulate a genuine blob-storage outage
    /// and assert the consumer retries rather than dead-letters.</summary>
    public bool ThrowOnUpload { get; set; }

    public IReadOnlyDictionary<string, byte[]> Blobs => _blobs;

    public Task<string> UploadAsync(string containerName, string blobName, Stream content, string contentType, CancellationToken ct = default)
    {
        if (ThrowOnUpload) throw new InvalidOperationException("Simulated blob storage outage.");

        using var ms = new MemoryStream();
        content.CopyTo(ms);
        _blobs[Key(containerName, blobName)] = ms.ToArray();
        return Task.FromResult(GetPublicUrl(containerName, blobName));
    }

    public Task<byte[]?> DownloadAsync(string containerName, string blobName, CancellationToken ct = default) =>
        Task.FromResult(_blobs.TryGetValue(Key(containerName, blobName), out var bytes) ? bytes : null);

    public Task DeleteAsync(string containerName, string blobName, CancellationToken ct = default)
    {
        _blobs.Remove(Key(containerName, blobName));
        return Task.CompletedTask;
    }

    public string GetPublicUrl(string containerName, string blobName) =>
        $"https://fake-storage.blob.core.windows.net/{containerName}/{blobName}";

    private static string Key(string containerName, string blobName) => $"{containerName}/{blobName}";
}
