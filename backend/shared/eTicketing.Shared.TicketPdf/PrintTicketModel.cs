namespace eTicketing.Shared.TicketPdf;

/// <summary>
/// One physical ticket on a printed sheet. Deliberately not <see cref="TicketPdfModel"/>: a printed
/// ticket has no buyer, no purchase timestamp and no order behind it, but it does have a
/// human-readable stub number that an online ticket never gets.
/// </summary>
public sealed record PrintTicketModel(
    Guid TicketId,
    int SerialNumber,
    string QrPayload,
    string SectorName,
    string? TicketTypeName,
    decimal Price,
    DateOnly? ValidDate)
{
    /// <summary>The number printed on the stub, zero-padded to six digits ("#000482"). Serial
    /// numbers run per product and never restart, so this doubles as a box-office reference.</summary>
    public string StubNumber => SerialNumber.ToString("D6");

    /// <summary>Full ticket id, uppercased. Printed under the stub number because this is what gate
    /// staff type into the scanner's manual fallback when a camera will not read the code.</summary>
    public string Serial => TicketId.ToString().ToUpperInvariant();
}
