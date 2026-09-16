namespace eTicketing.Contracts.Events;

/// <summary>
/// eTicketing.Payment → eTicketing.Ticketing. A recurring billing period was paid for, so the
/// subscription's next parking ticket is due.
///
/// The hop exists because the renewal originates at the payment provider (Stripe charges the saved
/// card on its own schedule and tells us afterwards) while the Ticket and Subscription rows live in
/// Ticketing. Doing it over RabbitMQ rather than a synchronous Payment → Ticketing call matters:
/// webhooks must be answered fast, and a provider that retries on non-2xx would otherwise become
/// this platform's transaction manager.
/// </summary>
/// <param name="PeriodEnd">Inclusive last day, matching Ticket.ValidTo. Taken from the provider so
/// our records and the buyer's card statement can never disagree about what was paid for.</param>
public record SubscriptionRenewed(
    string ProviderSubscriptionId,
    decimal AmountPaid,
    string Currency,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateTime PaidAt);
