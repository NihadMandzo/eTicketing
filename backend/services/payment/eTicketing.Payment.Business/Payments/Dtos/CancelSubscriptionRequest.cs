namespace eTicketing.Payment.Business.Payments;

public record CancelSubscriptionRequest
{
    public string SubscriptionReference { get; init; } = string.Empty;

    /// <summary>True for a buyer-initiated cancellation -- they keep the period they already paid
    /// for. False only for compensation, where the subscription must end immediately.</summary>
    public bool AtPeriodEnd { get; init; } = true;

    /// <summary>Compensation only. Subscription invoices cannot be manual-capture, so an expired
    /// hold on that path means real money moved and has to come back.</summary>
    public bool RefundLastInvoice { get; init; }
}
