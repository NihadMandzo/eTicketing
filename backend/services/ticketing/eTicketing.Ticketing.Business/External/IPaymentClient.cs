namespace eTicketing.Ticketing.Business.External;

public enum PaymentChargeStatus { Succeeded, Failed }

/// <summary>Mirrors eTicketing.Payment.Business.Payments.PaymentResponse's JSON shape — duplicated
/// rather than shared across the service boundary, same convention as CatalogProductResponse.</summary>
public record PaymentChargeResponse(Guid Id, decimal Amount, PaymentChargeStatus Status, string OrderRef);

/// <summary>HTTP client interface to eTicketing.Payment's only endpoint (POST /payments,
/// internal-only, never routed through the Gateway) — called synchronously from PurchaseService on
/// the purchase-critical path. Interface lives in .Business per .claude/rules/10-backend.md; the
/// concrete HttpClient-backed implementation (registered with the project's flagship circuit
/// breaker) lives in .Api/Infrastructure.</summary>
public interface IPaymentClient
{
    Task<PaymentChargeResponse> ChargeAsync(decimal amount, string orderRef, string cardNumberLast4, CancellationToken ct = default);
}

/// <summary>Thrown by the concrete HttpPaymentClient whenever the resilience pipeline gives up
/// (circuit open, timeout, or a genuine transport failure) — a plain, framework-agnostic exception
/// so PurchaseService (in .Business) never needs to reference Polly's types directly; those stay
/// confined to .Api/Infrastructure per .claude/rules/10-backend.md's layering.</summary>
public class PaymentUnavailableException : Exception
{
    public PaymentUnavailableException(string message, Exception innerException) : base(message, innerException) { }
}
