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

    /// <summary>Reads a blob back as bytes, or null if it no longer exists. Needed because
    /// eTicketing.Notifications attaches ticket PDFs to Brevo as inline base64 content rather than
    /// as a URL for Brevo to fetch — see BrevoEmailSender for why the URL form was abandoned.</summary>
    Task<byte[]?> DownloadAsync(string containerName, string blobName, CancellationToken ct = default);

    /// <summary>Deletes the blob if it exists — a no-op, not an error, if it doesn't (mirrors hard-delete cleanup on entity delete).</summary>
    Task DeleteAsync(string containerName, string blobName, CancellationToken ct = default);

    /// <summary>Pure URL construction, no network call — used to compute IconUrl/LogoUrl for every
    /// row of a paged list from a persisted blob name, without hitting Azure once per row.</summary>
    string GetPublicUrl(string containerName, string blobName);
}
