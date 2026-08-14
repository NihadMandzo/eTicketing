using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace eTicketing.Shared.Storage;

public static class BlobStorageServiceCollectionExtensions
{
    /// <summary>Binds BlobStorageOptions from the "BlobStorage" config section and registers
    /// IBlobStorageService as a singleton — BlobServiceClient is thread-safe and meant to be
    /// reused for the lifetime of the app, same reasoning as a typical HttpClient singleton.</summary>
    public static WebApplicationBuilder AddAzureBlobStorage(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<BlobStorageOptions>()
            .Bind(builder.Configuration.GetSection(BlobStorageOptions.SectionName));

        builder.Services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();

        return builder;
    }
}
