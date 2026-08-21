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
    private readonly ILogger<HttpPaymentClient> _logger;

    public HttpPaymentClient(HttpClient httpClient, ILogger<HttpPaymentClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PaymentChargeResponse> ChargeAsync(decimal amount, string orderRef, string cardNumberLast4, CancellationToken ct = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(
                "/payments",
                new { Amount = amount, OrderRef = orderRef, CardNumberLast4 = cardNumberLast4 },
                ct);
        }
        // Only a genuine transport failure (no status code at all) or the resilience pipeline
        // giving up counts as "Payment unavailable" — that's the only case where it's safe to say
        // the charge never happened and the caller may release the hold / show a retry message.
        catch (Exception ex) when (ex is BrokenCircuitException or TimeoutRejectedException or HttpRequestException { StatusCode: null })
        {
            throw new PaymentUnavailableException("Payment servis trenutno nije dostupan.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            // A real 4xx/5xx means Payment was reachable and answered — that's a different failure
            // mode than "unavailable" (e.g. a validation error on our own request) and must not be
            // relabeled as retryable/unavailable, which would tell the caller it's safe to release
            // the hold when Payment may already have processed something.
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "Payment servis je odgovorio sa {StatusCode} za OrderRef {OrderRef}: {Body}",
                response.StatusCode, orderRef, body);
            response.EnsureSuccessStatusCode();
        }

        try
        {
            return (await response.Content.ReadFromJsonAsync<PaymentChargeResponse>(cancellationToken: ct))!;
        }
        catch (Exception ex)
        {
            // The charge itself already succeeded on Payment's side by this point — a malformed
            // response body must not be swallowed as "unavailable" (which would look retryable);
            // log it clearly and let it bubble up as the genuine bug it is.
            _logger.LogError(ex, "Payment servis je vratio nevažeći odgovor za OrderRef {OrderRef}.", orderRef);
            throw;
        }
    }
}
