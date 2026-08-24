using eTicketing.Shared.Storage;

namespace eTicketing.Notifications.Tests.TestSupport;

/// <summary>
/// In-memory IBlobStorageService test double — Azure Blob Storage is a genuine external system per
/// the testing conventions, so it's faked rather than reached.
///
/// Notifications only ever reads: it pulls each ticket PDF back out of the "ticket-pdfs" container
/// so BrevoEmailSender can attach the bytes inline. Tests therefore <see cref="Seed"/> the blobs
/// PdfGeneration would have written, and a blob left unseeded stands in for one that has gone
/// missing between generation and send.
/// </summary>
public sealed class FakeBlobStorageService : IBlobStorageService
{
    private readonly Dictionary<string, byte[]> _blobs = [];

    public void Seed(string containerName, string blobName, byte[] content) =>
        _blobs[Key(containerName, blobName)] = content;

    public Task<byte[]?> DownloadAsync(string containerName, string blobName, CancellationToken ct = default) =>
        Task.FromResult(_blobs.TryGetValue(Key(containerName, blobName), out var bytes) ? bytes : null);

    public Task<string> UploadAsync(string containerName, string blobName, Stream content, string contentType, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        _blobs[Key(containerName, blobName)] = ms.ToArray();
        return Task.FromResult(GetPublicUrl(containerName, blobName));
    }

    public Task DeleteAsync(string containerName, string blobName, CancellationToken ct = default)
    {
        _blobs.Remove(Key(containerName, blobName));
        return Task.CompletedTask;
    }

    public string GetPublicUrl(string containerName, string blobName) =>
        $"https://fake-storage.blob.core.windows.net/{containerName}/{blobName}";

    private static string Key(string containerName, string blobName) => $"{containerName}/{blobName}";
}
