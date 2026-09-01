using System.Net;
using System.Net.Http.Json;
using eTicketing.Ticketing.Business.External;

namespace eTicketing.Ticketing.Api.Infrastructure;

/// <summary>Calls eTicketing.Catalog's internal-only product endpoints — never routed through the
/// Gateway, reachable only service-to-service. Registered with a retry+timeout resilience pipeline
/// (no circuit breaker here — that's reserved for the Ticketing→Payment call) in
/// TicketingServiceCollectionExtensions.</summary>
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

    public async Task<IReadOnlyList<CatalogProductResponse>> GetProductsAsync(IReadOnlyList<Guid> productIds, CancellationToken ct = default)
    {
        // A POST for a read is intentional: the id list is unbounded (an organizer can have dozens
        // of products live on one day) and would otherwise have to be crammed into a query string.
        if (productIds.Count == 0)
            return [];

        var response = await _httpClient.PostAsJsonAsync("/internal/products/by-ids", productIds, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<CatalogProductResponse>>(cancellationToken: ct) ?? [];
    }

    public async Task<IReadOnlyList<CatalogOrganizationProductStats>> GetOrganizationProductStatsAsync(
        CancellationToken ct = default)
    {
        // A plain GET, unlike the two above: this one takes no arguments at all. The Organizacije
        // report is platform-staff-only and always covers every organization, so there is no id
        // list to send.
        var response = await _httpClient.GetAsync("/internal/products/organization-stats", ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<CatalogOrganizationProductStats>>(cancellationToken: ct) ?? [];
    }

    public async Task<IReadOnlyList<CatalogProductResponse>> GetUpcomingProductsAsync(
        Guid? organizationId, int count, CancellationToken ct = default)
    {
        var query = organizationId is null
            ? $"/internal/products/upcoming?count={count}"
            : $"/internal/products/upcoming?organizationId={organizationId}&count={count}";

        var response = await _httpClient.GetAsync(query, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<CatalogProductResponse>>(cancellationToken: ct) ?? [];
    }
}
