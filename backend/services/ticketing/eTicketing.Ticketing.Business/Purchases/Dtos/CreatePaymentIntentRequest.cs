namespace eTicketing.Ticketing.Business.Purchases;

/// <summary>Prices the held reservation server-side and asks eTicketing.Payment for the intent the
/// buyer will confirm. Runs exactly the same validation POST /purchases does, so an expired hold or
/// a bad line item is caught before any payment object exists.</summary>
public record CreatePaymentIntentRequest
{
    public string HoldId { get; init; } = string.Empty;
    public IReadOnlyList<PurchaseLineItemRequest> LineItems { get; init; } = [];
}
