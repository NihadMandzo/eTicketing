namespace eTicketing.Payment.Business.Payments;

public record CancelIntentRequest
{
    public string IntentId { get; init; } = string.Empty;
}
