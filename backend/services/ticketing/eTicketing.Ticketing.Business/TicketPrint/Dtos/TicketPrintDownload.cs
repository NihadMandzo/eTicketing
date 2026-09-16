namespace eTicketing.Ticketing.Business.TicketPrint;

/// <summary>A rendered batch, ready to be streamed to the organizer exactly once.</summary>
public sealed record TicketPrintDownload(byte[] Content, string FileName);
