namespace eTicketing.Shared.Storage;

/// <summary>
/// Appends a version token to a public blob URL so overwriting a blob in place is visible to
/// clients.
///
/// <para><b>Why this is needed.</b> Logos and category icons are replaced by re-uploading to the
/// <i>same</i> blob key (deliberately — the key is fixed at first upload and does not track later
/// name changes), so <see cref="IBlobStorageService.GetPublicUrl"/> returns a byte-for-byte
/// identical URL before and after a replace. Every client caches on that URL: Flutter's
/// ImageCache keys <c>NetworkImage</c> on <c>(url, scale)</c>, and browsers key their HTTP cache
/// on it too. The result was a replaced image that never visibly changed until the app was
/// restarted, no matter how many times the entity itself was refetched.</para>
///
/// <para>The token is the entity's <c>UpdatedAt</c>, which the audit interceptor advances on every
/// save — including the save the replace path performs precisely so this value moves. Editing an
/// unrelated field also changes it, costing one harmless refetch; that is much cheaper than the
/// alternative of a stale image.</para>
///
/// <para>Not applicable to product images: each one is its own row with a key built from its own
/// id, so a "replaced" product image is a new blob at a new URL already.</para>
/// </summary>
public static class BlobUrlVersioning
{
    /// <summary>Query-parameter name. Ignored by Azure Blob Storage, which serves the blob
    /// regardless of unknown query parameters — it exists only to change the cache key.</summary>
    public const string VersionParameter = "v";

    public static string WithVersion(string url, DateTime updatedAtUtc)
    {
        var separator = url.Contains('?') ? '&' : '?';
        return $"{url}{separator}{VersionParameter}={updatedAtUtc.Ticks}";
    }
}
