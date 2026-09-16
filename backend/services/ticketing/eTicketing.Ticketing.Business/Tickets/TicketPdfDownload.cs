namespace eTicketing.Ticketing.Business.Tickets;

/// <summary>A rendered ticket, ready to be streamed to the browser.</summary>
public sealed record TicketPdfDownload(byte[] Content, string FileName);
