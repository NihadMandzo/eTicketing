using System.Net;
using System.Net.Http.Json;
using eTicketing.Ticketing.Business.External;

namespace eTicketing.Ticketing.Api.Infrastructure;

/// <summary>Calls eTicketing.Catalog's internal-only GET /internal/products/{id} — never routed
/// through the Gateway, reachable only service-to-service. Registered with a retry+timeout
/// resilience pipeline (no circuit breaker here — that's reserved for the future
/// Ticketing→Payment call) in TicketingServiceCollectionExtensions.</summary>
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
