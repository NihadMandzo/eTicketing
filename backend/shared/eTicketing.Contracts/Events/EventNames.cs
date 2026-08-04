namespace eTicketing.Contracts.Events;

public static class EventNames
{
    public const string Exchange = "eticketing.events";

    public const string TicketPurchased = "ticket.purchased";
    public const string VerificationEmailRequested = "verification-email.requested";
    public const string PaymentFailed = "payment.failed";
}
