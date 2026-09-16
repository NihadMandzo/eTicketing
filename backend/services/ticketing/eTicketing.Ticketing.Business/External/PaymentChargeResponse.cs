namespace eTicketing.Ticketing.Business.External;

/// <summary>Mirrors eTicketing.Payment.Business.Payments.PaymentResponse's JSON shape — duplicated
/// rather than shared across the service boundary, same convention as CatalogProductResponse.</summary>
public record PaymentChargeResponse(
    Guid Id,
    decimal Amount,
    PaymentChargeStatus Status,
    string OrderRef,
    string Currency,
    string? FailureCode);
