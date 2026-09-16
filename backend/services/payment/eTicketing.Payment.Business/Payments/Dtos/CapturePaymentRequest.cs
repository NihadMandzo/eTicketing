namespace eTicketing.Payment.Business.Payments;

public record CapturePaymentRequest
{
    public string IntentId { get; init; } = string.Empty;
    public string OrderRef { get; init; } = string.Empty;
    public Guid UserId { get; init; }
    public decimal ExpectedAmount { get; init; }

    /// <summary>Mock provider only: "0000" simulates a decline. Ignored entirely when the Stripe
    /// provider is configured.</summary>
    public string? SimulatedLast4 { get; init; }
}
