using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using eTicketing.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace eTicketing.Services.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly string _connectionString;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(IConfiguration configuration, ILogger<BlobStorageService> logger)
    {
        _connectionString = configuration.GetConnectionString("AzureBlobStorage") 
            ?? throw new ArgumentNullException(nameof(configuration), "AzureBlobStorage connection string is not configured");
        _logger = logger;
    }

    public async Task<string> UploadAsync(IFormFile file, string containerName)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty or null", nameof(file));

        // Create a unique filename
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        
        // Get a reference to a container
        var container = new BlobContainerClient(_connectionString, containerName);
        
        // Create the container if it doesn't already exist with private access
        // Note: Blobs are not publicly accessible. Implement SAS token generation for authorized access.
        await container.CreateIfNotExistsAsync(PublicAccessType.None);
        
        // Get a reference to the blob
        var blob = container.GetBlobClient(fileName);
        
        // Upload the file
        using (var stream = file.OpenReadStream())
        {
            await blob.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType });
        }
        
        return blob.Uri.ToString();
    }

    public async Task DeleteAsync(string blobUrl, string containerName)
    {
        if (string.IsNullOrEmpty(blobUrl))
            return;

        try
        {
            // Get the blob name from the URL
            var uri = new Uri(blobUrl);
            var blobName = Path.GetFileName(uri.LocalPath);
            
            // Get a reference to a container
            var container = new BlobContainerClient(_connectionString, containerName);
            
            // Get a reference to the blob
            var blob = container.GetBlobClient(blobName);
            
            // Delete the blob
            await blob.DeleteIfExistsAsync();
        }
        catch (Exception ex)
        {
            // Log the exception but don't throw to prevent cascading failures
            _logger.LogError(ex, "Failed to delete blob from container {ContainerName}: {BlobUrl}", containerName, blobUrl);
        }
    }
}
