namespace eTicketing.Payment.Business.Payments.Gateways;

public record GatewayIntentResult(
    string IntentId,
    string? ClientSecret,
    GatewayPaymentStatus Status,
    long AmountMinor,
    string Currency);
