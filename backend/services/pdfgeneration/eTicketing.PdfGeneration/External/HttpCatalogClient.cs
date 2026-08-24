using System.Net;
using System.Net.Http.Json;

namespace eTicketing.PdfGeneration.External;

/// <summary>Calls eTicketing.Catalog's internal-only GET /internal/products/{id} — never routed
/// through the Gateway, reachable only service-to-service. Registered with a retry+timeout
/// resilience pipeline in PdfGenerationServiceCollectionExtensions.</summary>
public class HttpCatalogClient : ICatalogClient
{
    private readonly HttpClient _httpClient;

    public HttpCatalogClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<CatalogProductResponse?> GetProductAsync(Guid productId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"/internal/products/{productId}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CatalogProductResponse>(cancellationToken: ct);
    }
}
