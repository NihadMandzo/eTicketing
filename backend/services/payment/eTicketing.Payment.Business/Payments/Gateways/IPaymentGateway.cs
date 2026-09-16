namespace eTicketing.Payment.Business.Payments.Gateways;

/// <summary>
/// The seam between this service's orchestration/persistence and whichever payment provider is
/// configured. Implementations do provider calls only, never database work, which is what lets
/// every unit test in eTicketing.Payment.Business.Tests mock this interface and stay entirely off
/// the network.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>"Mock" or "Stripe". Travels back to the clients so they know which payment form to
    /// render: see PaymentOptions for why that matters.</summary>
    string Name { get; }

    /// <summary>Null for the mock. The clients receive this instead of compiling the key in, so
    /// rotating it needs no rebuild.</summary>
    string? PublishableKey { get; }

    Task<GatewayIntentResult> CreateIntentAsync(GatewayIntentRequest request, CancellationToken ct = default);

    /// <summary>Re-reads the intent from the provider and verifies it against the caller's own
    /// claims before capturing. Implementations must never trust the request alone.</summary>
    Task<GatewayCaptureResult> CaptureAsync(GatewayCaptureRequest request, CancellationToken ct = default);

    /// <summary>Voids an uncaptured authorization. Best-effort compensation for the case where the
    /// buyer confirmed but the Redis hold expired before POST /purchases arrived: no money ever
    /// moved, so this is a cancel rather than a refund.</summary>
    Task CancelIntentAsync(string intentId, CancellationToken ct = default);

    /// <summary>Creates the provider-side customer a subscription bills against. PaymentService
    /// calls this only when it has no stored customer for the buyer, and persists the result.</summary>
    Task<string> CreateCustomerAsync(Guid userId, string email, CancellationToken ct = default);

    Task<GatewaySubscriptionResult> CreateSubscriptionAsync(GatewaySubscriptionRequest request, CancellationToken ct = default);

    /// <summary>Server-side verification that the client really did pay the first invoice.
    /// Symmetric with CaptureAsync, including the mock-only simulatedLast4 signal, because a
    /// subscription's period one is confirmed at exactly the same point in the flow that a
    /// one-time payment is captured.</summary>
    Task<GatewaySubscriptionResult> ConfirmSubscriptionAsync(
        string subscriptionId, string? simulatedLast4, CancellationToken ct = default);

    /// <param name="atPeriodEnd">True for a buyer-initiated cancellation: they keep the period they
    /// already paid for. False only for compensation, where the subscription must die now.</param>
    Task CancelSubscriptionAsync(string subscriptionId, bool atPeriodEnd, CancellationToken ct = default);

    /// <summary>Subscription invoices cannot be manual-capture, so that one path needs a real
    /// refund when its hold expires after payment. The one-time path never calls this.</summary>
    Task RefundAsync(string paymentIntentId, CancellationToken ct = default);
}
