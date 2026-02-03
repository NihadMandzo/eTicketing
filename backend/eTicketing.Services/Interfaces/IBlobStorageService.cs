using Microsoft.AspNetCore.Http;

namespace eTicketing.Services.Interfaces;

public interface IBlobStorageService
{
    Task<string> UploadAsync(IFormFile file, string containerName);
    Task DeleteAsync(string blobUrl, string containerName);
}
