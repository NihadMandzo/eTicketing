namespace eTicketing.Shared.Storage;

/// <summary>
/// Thin wrapper over Azure Blob Storage — the only storage mechanism for user-uploaded images
/// (category icons, organization logos, later event images) in this app; no bytes are stored in
/// any service's SQL database anymore. One storage account, one container per image kind
/// ("category-icons", "organization-logos", "event-images"), containers created lazily on first
/// upload with anonymous blob-level read access so IconUrl/LogoUrl can point straight at Azure.
/// </summary>
public interface IBlobStorageService
{
    /// <summary>Uploads (overwriting if the blob already exists) and returns the blob's public URL.</summary>
    Task<string> UploadAsync(string containerName, string blobName, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>Uploads (overwriting if the blob already exists) into a container created WITHOUT
    /// anonymous read access, and returns nothing — a private blob has no usable public URL. For
    /// artifacts that aren't user-facing images: currently only the trained recommendation model,
    /// which is derived from users' purchase behavior and must not be world-readable the way a
    /// category icon deliberately is. Read it back with <see cref="DownloadAsync"/>.</summary>
    Task UploadPrivateAsync(string containerName, string blobName, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>Reads a blob back into a stream the caller owns and must dispose. Returns null —
    /// not an exception — when the container or blob doesn't exist: a first boot before anything
    /// has been uploaded is an expected state, not a failure.</summary>
    Task<Stream?> DownloadAsync(string containerName, string blobName, CancellationToken ct = default);

    /// <summary>Deletes the blob if it exists — a no-op, not an error, if it doesn't (mirrors hard-delete cleanup on entity delete).</summary>
    Task DeleteAsync(string containerName, string blobName, CancellationToken ct = default);

    /// <summary>Pure URL construction, no network call — used to compute IconUrl/LogoUrl for every
    /// row of a paged list from a persisted blob name, without hitting Azure once per row.</summary>
    string GetPublicUrl(string containerName, string blobName);
}
