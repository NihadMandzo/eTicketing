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
