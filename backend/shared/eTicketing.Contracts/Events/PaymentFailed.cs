namespace eTicketing.Contracts.Events;

/// <summary>
/// eTicketing.Ticketing → eTicketing.Notifications. A buyer's card was declined at capture: the hold
/// has already been released and nothing was charged, so this is purely "here is what happened and
/// what to do about it", never a money-recovery notice.
/// </summary>
/// <param name="Reason">The payment provider's own failure code, verbatim (<c>card_declined</c>,
/// <c>insufficient_funds</c>, ...). Deliberately kept raw on the wire so logs and any future
/// analysis see exactly what the provider said — the consumer maps it to Bosnian and is careful
/// about which codes it is willing to repeat back, since some of them (a card reported lost or
/// stolen) say something about the cardholder that the address on the order may not be entitled to
/// know.</param>
/// <param name="OrderId">Which attempt failed, so the buyer can tell two tries apart and support
/// can find it. Optional only for the deploy window, where an event published by the previous
/// version is still in flight — see TicketPurchased for the same pattern.</param>
public record PaymentFailed(
    Guid UserId,
    string UserEmail,
    string Reason,
    DateTime FailedAt,
    Guid? OrderId = null,
    string? SectorName = null,
    decimal? Amount = null);
