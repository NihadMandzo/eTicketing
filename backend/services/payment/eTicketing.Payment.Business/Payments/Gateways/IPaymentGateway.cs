namespace eTicketing.Payment.Business.Payments.Gateways;

/// <summary>
/// Provider-side lifecycle of a payment, normalized across gateways. Deliberately internal to
/// eTicketing.Payment.Business: it is never serialized across a service boundary, so unlike
/// PaymentStatus (whose ordinals are the wire format between Payment and Ticketing, guarded by
/// PaymentEnumParityTests) this enum can be reordered freely.
/// </summary>
public enum GatewayPaymentStatus
{
    /// <summary>Created, nothing entered yet: the state a fresh intent is handed to the client in.</summary>
    RequiresPaymentMethod,

    /// <summary>The buyer must complete 3-D Secure. Only ever seen client-side here, since
    /// AllowRedirects is "never" and Stripe.js resolves the challenge in its own in-page modal.</summary>
    RequiresAction,

    /// <summary>Authorized but not captured: the money is ring-fenced on the card and nothing has
    /// moved. This is the state a one-time intent is in when the buyer returns from
    /// stripe.confirmPayment, and the reason CaptureMethod is "manual" -- if the Redis hold expired
    /// in the meantime we cancel instead of refunding.</summary>
    RequiresCapture,

    Succeeded,

    /// <summary>Declined, voided, or failed verification. A normal business outcome, never an
    /// exception: see IPaymentService for why this stays a successful Result.</summary>
    Failed,

    Cancelled,
}

/// <param name="Amount">In major units (the KM figure the UI shows). Converted to the provider's
/// integer minor units exactly once, by MoneyConverter.</param>
/// <param name="Metadata">Written onto the provider object and read back at capture time to prove
/// the intent belongs to this order and this buyer. The client is never believed on its own.</param>
public record GatewayIntentRequest(
    decimal Amount,
    string Currency,
    string OrderRef,
    Guid UserId,
    string? CustomerEmail,
    string Description,
    IReadOnlyDictionary<string, string> Metadata);

public record GatewayIntentResult(
    string IntentId,
    string? ClientSecret,
    GatewayPaymentStatus Status,
    long AmountMinor,
    string Currency);

/// <param name="SimulatedLast4">Mock provider only: "0000" simulates a decline, so the
/// declined-card and circuit-breaker demos work with no Stripe account at all. StripePaymentGateway
/// ignores it entirely.</param>
public record GatewayCaptureRequest(
    string IntentId,
    string OrderRef,
    Guid UserId,
    decimal ExpectedAmount,
    string ExpectedCurrency,
    string? SimulatedLast4);

/// <param name="FailureCode">Stripe's decline_code, or its error code when there is no
/// decline_code. Carried through so the clients can map it to a specific Bosnian message instead
/// of one generic failure text.</param>
public record GatewayCaptureResult(
    GatewayPaymentStatus Status,
    string? FailureCode,
    long AmountMinor,
    string Currency);

/// <param name="CustomerReference">Already-resolved provider customer ("cus_..."), looked up or
/// created by PaymentService and persisted on StripeCustomer. Passing it in rather than resolving it
/// here is what stops a buyer who subscribes twice from ending up with two provider customers and
/// two saved cards.</param>
public record GatewaySubscriptionRequest(
    decimal AmountPerPeriod,
    string Currency,
    string OrderRef,
    Guid UserId,
    string CustomerReference,
    string ProductName,
    IReadOnlyDictionary<string, string> Metadata);

/// <param name="ClientSecret">Of the first invoice's PaymentIntent. The subscription is created
/// with payment_behavior=default_incomplete precisely so period one is a real invoice the client
/// confirms with the same Elements instance a one-time purchase uses, which keeps period one
/// structurally identical to every renewal.</param>
public record GatewaySubscriptionResult(
    string SubscriptionId,
    string? ClientSecret,
    string? InvoicePaymentIntentId,
    GatewayPaymentStatus Status,
    string? FailureCode,
    DateOnly CurrentPeriodStart,
    DateOnly CurrentPeriodEnd);

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

/// <summary>
/// Thrown when the provider itself is unreachable or broken (5xx, connection error, rate limit),
/// as opposed to a decline, which is a perfectly normal answer. PaymentService turns this into
/// Error.Failure("payment.provider_unavailable"), which ResultExtensions already maps to 503, so
/// Ticketing's HttpPaymentClient sees the same "unavailable" shape it already handles for a
/// stopped container and releases the hold.
/// </summary>
public class PaymentGatewayUnavailableException : Exception
{
    public PaymentGatewayUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}

public static class GatewayMetadataKeys
{
    public const string OrderRef = "order_ref";
    public const string UserId = "user_id";
    public const string HoldRef = "hold_ref";
    public const string SectorId = "sector_id";
}
