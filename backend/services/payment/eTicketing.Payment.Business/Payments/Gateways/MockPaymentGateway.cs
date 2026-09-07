namespace eTicketing.Payment.Business.Payments.Gateways;

/// <summary>
/// The offline gateway, and the reason PAYMENT_PROVIDER=Mock is a real rollback rather than an
/// aspiration: it keeps the deterministic behavior this service shipped with before Stripe
/// existed -- a card whose last four digits are "0000" declines, anything else succeeds -- so the
/// declined-card path and the Ticketing circuit-breaker demo both work with no Stripe account, no
/// API key and no network. See docs/arhitektura-migracija-mikroservisi-eda.md section 9, which
/// names Stripe the second thing to sacrifice and this the documented fallback.
///
/// It answers the same intent/capture/subscription calls the Stripe gateway does, so the two
/// providers are interchangeable behind one set of endpoints and Ticketing never learns which is
/// configured.
/// </summary>
public class MockPaymentGateway : IPaymentGateway
{
    /// <summary>The last-four that simulates a decline. Mirrored in the Bosnian hint text on the
    /// web checkout and the mobile payment screen, which both say so out loud in Mock mode.</summary>
    public const string DecliningLast4 = "0000";

    public string Name => PaymentProviderNames.Mock;

    /// <summary>No key: there is no client-side SDK to initialize. The clients read this being
    /// null (together with Name) as "render the plain card form".</summary>
    public string? PublishableKey => null;

    public Task<GatewayIntentResult> CreateIntentAsync(GatewayIntentRequest request, CancellationToken ct = default)
    {
        var id = $"pi_mock_{Guid.NewGuid():N}";

        return Task.FromResult(new GatewayIntentResult(
            id,
            // Shaped like Stripe's so the clients need no special case, but it unlocks nothing --
            // no mock client ever calls a payment SDK with it.
            $"{id}_secret_mock",
            GatewayPaymentStatus.RequiresPaymentMethod,
            MoneyConverter.ToMinorUnits(request.Amount),
            request.Currency));
    }

    public Task<GatewayCaptureResult> CaptureAsync(GatewayCaptureRequest request, CancellationToken ct = default)
    {
        var declined = request.SimulatedLast4 == DecliningLast4;

        return Task.FromResult(new GatewayCaptureResult(
            declined ? GatewayPaymentStatus.Failed : GatewayPaymentStatus.Succeeded,
            declined ? "card_declined" : null,
            MoneyConverter.ToMinorUnits(request.ExpectedAmount),
            request.ExpectedCurrency));
    }

    public Task CancelIntentAsync(string intentId, CancellationToken ct = default) => Task.CompletedTask;

    public Task<string> CreateCustomerAsync(Guid userId, string email, CancellationToken ct = default)
        => Task.FromResult($"cus_mock_{userId:N}");

    public Task<GatewaySubscriptionResult> CreateSubscriptionAsync(GatewaySubscriptionRequest request, CancellationToken ct = default)
    {
        var id = $"sub_mock_{Guid.NewGuid():N}";
        var (start, end) = MockPeriod();

        return Task.FromResult(new GatewaySubscriptionResult(
            id,
            $"pi_mock_{Guid.NewGuid():N}_secret_mock",
            $"pi_mock_{Guid.NewGuid():N}",
            GatewayPaymentStatus.RequiresPaymentMethod,
            null,
            start,
            end));
    }

    public Task<GatewaySubscriptionResult> ConfirmSubscriptionAsync(
        string subscriptionId, string? simulatedLast4, CancellationToken ct = default)
    {
        var declined = simulatedLast4 == DecliningLast4;
        var (start, end) = MockPeriod();

        return Task.FromResult(new GatewaySubscriptionResult(
            subscriptionId,
            null,
            null,
            declined ? GatewayPaymentStatus.Failed : GatewayPaymentStatus.Succeeded,
            declined ? "card_declined" : null,
            start,
            end));
    }

    public Task CancelSubscriptionAsync(string subscriptionId, bool atPeriodEnd, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task RefundAsync(string paymentIntentId, CancellationToken ct = default) => Task.CompletedTask;

    /// <summary>
    /// One calendar month from today, matching the period Stripe would return. UtcNow rather than
    /// PlatformClock because this is a stand-in for a provider's own clock, not a business date --
    /// PurchaseService still stamps the Subscription row from the period the gateway reports.
    /// </summary>
    private static (DateOnly Start, DateOnly End) MockPeriod()
    {
        var start = DateOnly.FromDateTime(DateTime.UtcNow);
        return (start, start.AddMonths(1).AddDays(-1));
    }
}
