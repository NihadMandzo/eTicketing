namespace eTicketing.Payment.Business.Payments.Gateways;

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
