namespace eTicketing.Ticketing.Data.Entities;

/// <summary>
/// The rendered sheet for one <see cref="TicketPrintBatch"/>, in its own table so the batch row
/// stays cheap to read.
///
/// <para><b>Deliberately transient.</b> Ticket PDFs are never archived anywhere in this platform —
/// that is why GET /tickets/{id}/pdf re-renders per request instead of reading a stored file — and
/// this sheet is worse than one ticket: it is thousands of working gate codes in a single file. So
/// this row exists only in the gap between "the worker finished" and "the organizer downloaded
/// it". It is deleted the moment the bytes are handed over, and swept away if nobody ever collects
/// them. Nothing is ever written to blob storage.</para>
/// </summary>
public class TicketPrintBatchFile
{
    // PK and FK at once — a batch has at most one file.
    public Guid BatchId { get; set; }
    public TicketPrintBatch? Batch { get; set; }

    public byte[] Content { get; set; } = [];
}
