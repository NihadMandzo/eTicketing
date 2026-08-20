using System.Net.Http.Json;
using eTicketing.Ticketing.Business.External;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace eTicketing.Ticketing.Api.Infrastructure;

/// <summary>Calls eTicketing.Payment's only endpoint (POST /payments), protected by the project's
/// flagship circuit breaker (registered in TicketingServiceCollectionExtensions) since this is the
/// one call on the synchronous purchase-critical path. Translates the resilience pipeline's Polly
/// exceptions into the plain PaymentUnavailableException so callers in .Business never need to
/// reference Polly directly.</summary>
public class HttpPaymentClient : IPaymentClient
{
    private readonly HttpClient _httpClient;

    public HttpPaymentClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<PaymentChargeResponse> ChargeAsync(decimal amount, string orderRef, string cardNumberLast4, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/payments",
                new { Amount = amount, OrderRef = orderRef, CardNumberLast4 = cardNumberLast4 },
                ct);
            response.EnsureSuccessStatusCode();

            return (await response.Content.ReadFromJsonAsync<PaymentChargeResponse>(cancellationToken: ct))!;
        }
        catch (Exception ex) when (ex is BrokenCircuitException or TimeoutRejectedException or HttpRequestException)
        {
            throw new PaymentUnavailableException("Payment servis trenutno nije dostupan.", ex);
        }
    }
}
