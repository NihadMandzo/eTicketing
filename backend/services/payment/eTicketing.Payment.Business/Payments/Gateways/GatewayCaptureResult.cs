namespace eTicketing.Payment.Business.Payments.Gateways;

/// <param name="FailureCode">Stripe's decline_code, or its error code when there is no
/// decline_code. Carried through so the clients can map it to a specific Bosnian message instead
/// of one generic failure text.</param>
public record GatewayCaptureResult(
    GatewayPaymentStatus Status,
    string? FailureCode,
    long AmountMinor,
    string Currency);
