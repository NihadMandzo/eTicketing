using QRCoder;

namespace eTicketing.Shared.TicketPdf;

/// <summary>
/// Turns a ticket's signed QR payload into PNG bytes for embedding in the PDF.
///
/// This service never signs anything — the payload arrives already minted on the TicketPurchased
/// event, because eTicketing.Ticketing is the only holder of the QR signing key. All this does is
/// draw it.
///
/// PngByteQRCode rather than QRCoder's System.Drawing-backed renderer: this runs in a Linux
/// container with no libgdiplus, where the latter throws at runtime.
/// </summary>
public static class QrCodeRenderer
{
    // 12 rather than the API response's 8 — this one gets printed, and a QR scanned off paper under
    // bad lighting benefits from the extra module size far more than the file size costs.
    private const int PixelsPerModule = 12;

    public static byte[] Render(string payload)
    {
        using var generator = new QRCodeGenerator();
        // ECC level Q (25% recovery) so a folded, smudged or partly-torn printout still scans.
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(PixelsPerModule);
    }
}
