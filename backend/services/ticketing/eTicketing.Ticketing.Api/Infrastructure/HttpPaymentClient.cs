using System.Net;
using System.Net.Http.Json;
using eTicketing.Ticketing.Business.External;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace eTicketing.Ticketing.Api.Infrastructure;

public class HttpPaymentClient : IPaymentClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpPaymentClient> _logger;

    public HttpPaymentClient(HttpClient httpClient, ILogger<HttpPaymentClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<PaymentIntentCreatedResponse> CreateIntentAsync(
        decimal amount,
        string orderRef,
        Guid userId,
        string userEmail,
        string description,
        string holdRef,
        Guid sectorId,
        bool isSubscription,
        string? subscriptionProductName,
        CancellationToken ct = default)
        => PostAsync<PaymentIntentCreatedResponse>(
            "/payments/intents",
            new
            {
                Amount = amount,
                OrderRef = orderRef,
                UserId = userId,
                CustomerEmail = userEmail,
                Description = description,
                HoldRef = holdRef,
                SectorId = sectorId,
                IsSubscription = isSubscription,
                SubscriptionProductName = subscriptionProductName,
            },
            orderRef,
            ct);

    public Task<PaymentChargeResponse> CapturePaymentAsync(
        string intentId,
        string orderRef,
        Guid userId,
        decimal expectedAmount,
        string? simulatedLast4,
        CancellationToken ct = default)
        => PostAsync<PaymentChargeResponse>(
            "/payments/capture",
            new
            {
                IntentId = intentId,
                OrderRef = orderRef,
                UserId = userId,
                ExpectedAmount = expectedAmount,
                SimulatedLast4 = simulatedLast4,
            },
            orderRef,
            ct);

    public Task<SubscriptionChargeResponse> ConfirmSubscriptionAsync(
        string subscriptionReference,
        string orderRef,
        Guid userId,
        decimal expectedAmount,
        string? simulatedLast4,
        CancellationToken ct = default)
        => PostAsync<SubscriptionChargeResponse>(
            "/payments/subscriptions/confirm",
            new
            {
                SubscriptionReference = subscriptionReference,
                OrderRef = orderRef,
                UserId = userId,
                ExpectedAmount = expectedAmount,
                SimulatedLast4 = simulatedLast4,
            },
            orderRef,
            ct);

    public Task CancelIntentAsync(string intentId, CancellationToken ct = default)
        => PostVoidAsync("/payments/intents/cancel", new { IntentId = intentId }, intentId, ct);

    public Task CancelSubscriptionAsync(
        string subscriptionReference, bool atPeriodEnd, bool refundLastInvoice, CancellationToken ct = default)
        => PostVoidAsync(
            "/payments/subscriptions/cancel",
            new
            {
                SubscriptionReference = subscriptionReference,
                AtPeriodEnd = atPeriodEnd,
                RefundLastInvoice = refundLastInvoice,
            },
            subscriptionReference,
            ct);

    private async Task<T> PostAsync<T>(string path, object payload, string reference, CancellationToken ct)
    {
        var response = await SendAsync(path, payload, reference, ct);

        try
        {
            return (await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct))!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payment servis je vratio nevažeći odgovor za {Reference}.", reference);
            throw;
        }
    }

    private async Task PostVoidAsync(string path, object payload, string reference, CancellationToken ct)
        => await SendAsync(path, payload, reference, ct);

    private async Task<HttpResponseMessage> SendAsync(string path, object payload, string reference, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(path, payload, ct);
        }
        // Only a genuine transport failure (no status code at all) or the resilience pipeline
        // giving up counts as "Payment unavailable" — that's the only case where it's safe to say
        // the charge never happened and the caller may release the hold / show a retry message.
        catch (Exception ex) when (ex is BrokenCircuitException or TimeoutRejectedException or HttpRequestException { StatusCode: null })
        {
            throw new PaymentUnavailableException("Payment servis trenutno nije dostupan.", ex);
        }

        // Payment answers 503 for exactly one thing: the payment PROVIDER is down (see
        // PaymentService's payment.provider_unavailable). That is the same class of outage as the
        // circuit opening above and the buyer should be told the same thing, so it is translated
        // here rather than bubbling as a generic 5xx.
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            _logger.LogWarning("Provajder plaćanja nije dostupan (503 od Payment servisa) za {Reference}.", reference);
            throw new PaymentUnavailableException("Provajder plaćanja trenutno nije dostupan.");
        }

        if (!response.IsSuccessStatusCode)
        {
            // A real 4xx or 5xx means Payment was reachable and answered — a different failure mode
            // from "unavailable", and it must not be relabelled as retryable, which would tell the
            // caller it is safe to release the hold when Payment may already have processed something.
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "Payment servis je odgovorio sa {StatusCode} za {Reference}: {Body}",
                response.StatusCode, reference, body);
            response.EnsureSuccessStatusCode();
        }

        return response;
    }
}
