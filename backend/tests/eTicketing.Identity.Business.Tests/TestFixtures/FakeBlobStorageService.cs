using eTicketing.Shared.Storage;

namespace eTicketing.Identity.Business.Tests.TestFixtures;

/// <summary>
/// In-memory IBlobStorageService test double — Azure Blob Storage is a genuine external system
/// per the testing conventions (mock it, don't hit real Azure from a unit test), but a real
/// implementation lets tests assert actual upload/delete/replace behavior (blob exists, then
/// doesn't) rather than just verifying calls were made.
/// </summary>
public sealed class FakeBlobStorageService : IBlobStorageService
{
    private readonly Dictionary<string, byte[]> _blobs = [];

    /// <summary>When set, DeleteAsync throws instead of deleting — lets tests simulate a genuine
    /// blob-storage outage to assert on delete-ordering (DB commit vs. blob delete).</summary>
    public bool ThrowOnDelete { get; set; }

    public Task<string> UploadAsync(string containerName, string blobName, Stream content, string contentType, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        _blobs[Key(containerName, blobName)] = ms.ToArray();
        return Task.FromResult(GetPublicUrl(containerName, blobName));
    }

    public Task DeleteAsync(string containerName, string blobName, CancellationToken ct = default)
    {
        if (ThrowOnDelete) throw new InvalidOperationException("Simulated blob storage outage.");

        _blobs.Remove(Key(containerName, blobName));
        return Task.CompletedTask;
    }

    public string GetPublicUrl(string containerName, string blobName) =>
        $"https://fake-storage.blob.core.windows.net/{containerName}/{blobName}";

    public bool Exists(string containerName, string blobName) => _blobs.ContainsKey(Key(containerName, blobName));

    private static string Key(string containerName, string blobName) => $"{containerName}/{blobName}";
}
