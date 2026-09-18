namespace eTicketing.Payment.Business.Payments.Gateways;

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
