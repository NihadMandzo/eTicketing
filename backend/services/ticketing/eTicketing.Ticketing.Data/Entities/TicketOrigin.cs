namespace eTicketing.Ticketing.Data.Entities;

/// <summary>How this ticket came into existence. Online tickets are bought through POST /purchases
/// and belong to a UserId; Printed tickets are minted in bulk by an organizer through the
/// physical-ticket export (see TicketPrintBatch) and have no buyer at all — they are sold over a
/// counter and the paper itself is the bearer token. Both kinds carry a real signed QR payload and
/// validate identically at the gate.
///
/// Persisted as the integer ordinal, same convention as TicketStatus — append only.</summary>
public enum TicketOrigin
{
    Online,
    Printed
}
