using System.Net.Http.Json;
using eTicketing.Ticketing.Business.External;

namespace eTicketing.Ticketing.Api.Infrastructure;

/// <summary>Calls eTicketing.Identity's internal-only organization endpoint — never routed through
/// the Gateway, reachable only service-to-service. Registered with the same retry+timeout
/// resilience pipeline as <see cref="HttpCatalogClient"/> (no circuit breaker: that stays reserved
/// for the Ticketing→Payment call on the purchase-critical path).</summary>
public class HttpIdentityClient : IIdentityClient
{
    private readonly HttpClient _httpClient;

    public HttpIdentityClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<IReadOnlyList<IdentityOrganizationResponse>> GetOrganizationsAsync(
        IReadOnlyList<Guid> organizationIds, CancellationToken ct = default)
    {
        // A POST for a read, same reasoning as HttpCatalogClient.GetProductsAsync: the id list is
        // unbounded (a platform-wide report covers every organization that has ever sold a ticket)
        // and would otherwise have to be crammed into a query string.
        if (organizationIds.Count == 0)
            return [];

        var response = await _httpClient.PostAsJsonAsync("/internal/organizations/by-ids", organizationIds, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<IdentityOrganizationResponse>>(cancellationToken: ct) ?? [];
    }
}
