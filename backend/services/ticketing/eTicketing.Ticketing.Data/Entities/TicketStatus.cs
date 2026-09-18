namespace eTicketing.Ticketing.Data.Entities;

/// <summary>Persisted as the integer ordinal (System.Text.Json default) and mirrored by ordinal in
/// every frontend enum table — frontend/web's TICKET_STATUSES and frontend/mobile's
/// ticketStatusNames. Append new values only; never reorder or remove.</summary>
public enum TicketStatus
{
    Processing,
    Confirmed,
    Ready,     // PDF generated (Sprint 4 flow)
    Cancelled,
    Used       // Scanned and admitted at the gate — terminal, see MarkValidated
}
