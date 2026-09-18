namespace eTicketing.Payment.Business.Payments;

public record ConfirmSubscriptionRequest
{
    public string SubscriptionReference { get; init; } = string.Empty;
    public string OrderRef { get; init; } = string.Empty;
    public Guid UserId { get; init; }
    public decimal ExpectedAmount { get; init; }
    public string? SimulatedLast4 { get; init; }
}
