namespace eTicketing.Shared.Storage;

/// <summary>
/// Bound from the "BlobStorage" configuration section — one Azure Storage account shared by
/// every service that owns an image (Catalog's category icons, Identity's organization logos,
/// later Ticketing's event images), each using its own container. ConnectionString comes from
/// the root .env's AZURE_STORAGE_CONNECTION_STRING, passed through docker-compose.yml as
/// BlobStorage__ConnectionString — never hardcoded here.
/// </summary>
public class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    public string ConnectionString { get; set; } = string.Empty;
}
