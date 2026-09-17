namespace eTicketing.Ticketing.Business.External;

/// <summary>Thrown by the concrete HttpPaymentClient whenever the resilience pipeline gives up
/// (circuit open, timeout, or a genuine transport failure), and also when Payment itself answers 503
/// because the payment provider is down — a plain, framework-agnostic exception so PurchaseService
/// (in .Business) never needs to reference Polly's types directly; those stay confined to
/// .Api/Infrastructure per .claude/rules/10-backend.md's layering.</summary>
public class PaymentUnavailableException : Exception
{
    public PaymentUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
