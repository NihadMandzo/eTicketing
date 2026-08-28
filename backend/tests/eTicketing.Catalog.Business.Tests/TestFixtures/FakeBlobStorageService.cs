using eTicketing.Shared.Storage;

namespace eTicketing.Catalog.Business.Tests.TestFixtures;

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

    /// <summary>Artificial time spent inside <see cref="UploadPrivateAsync"/>. Widens the window in
    /// which two uploads could overlap, so <see cref="MaxConcurrentUploads"/> is a meaningful
    /// observation rather than a coincidence of how fast the fake happens to be.</summary>
    public TimeSpan UploadDelay { get; set; } = TimeSpan.Zero;

    /// <summary>Highest number of uploads seen in flight at the same moment. A model that serializes
    /// its training runs can never exceed 1 here, whatever the caller does.</summary>
    public int MaxConcurrentUploads { get; private set; }

    private int _uploadsInFlight;
    private readonly Lock _gate = new();

    /// <summary>Stored in the same dictionary as public blobs — the fake doesn't model access
    /// levels, only content. What it does let a test assert is the round trip: upload a trained
    /// model, download it back, and confirm it still scores identically.</summary>
    public async Task UploadPrivateAsync(string containerName, string blobName, Stream content, string contentType, CancellationToken ct = default)
    {
        lock (_gate)
        {
            _uploadsInFlight++;
            MaxConcurrentUploads = Math.Max(MaxConcurrentUploads, _uploadsInFlight);
        }

        try
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, ct);

            if (UploadDelay > TimeSpan.Zero)
                await Task.Delay(UploadDelay, ct);

            lock (_gate)
            {
                _blobs[Key(containerName, blobName)] = ms.ToArray();
            }
        }
        finally
        {
            lock (_gate)
            {
                _uploadsInFlight--;
            }
        }
    }

    public Task<Stream?> DownloadAsync(string containerName, string blobName, CancellationToken ct = default) =>
        Task.FromResult(_blobs.TryGetValue(Key(containerName, blobName), out var bytes)
            ? (Stream)new MemoryStream(bytes)
            : null);

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
