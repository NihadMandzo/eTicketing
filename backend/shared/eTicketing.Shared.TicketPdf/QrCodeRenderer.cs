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

    /// <summary>
    /// The same code as scalable vector markup rather than pixels — what <see cref="PrintSheetDocument"/>
    /// uses for a batch of physical tickets.
    ///
    /// Two reasons, and the second is the one that matters at the gate. First, size: PDF has no
    /// native PNG support, so an embedded raster is re-encoded as a compressed pixel stream, and a
    /// 492x492 QR cost roughly 14 KB *per ticket* — 86% of a batch PDF was QR raster data, which is
    /// what put a 5000-ticket export beyond what could be held, stored and streamed. The vector
    /// form is a few hundred bytes of rectangles. Second, fidelity: a raster QR is resampled by
    /// whatever prints it, softening the module edges a scanner keys on, and it is fixed at one
    /// resolution. Vector modules stay exactly square at any printer DPI.
    /// </summary>
    public static string RenderSvg(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);

        // Size 1 per module: the SVG carries its own viewBox, and QuestPDF scales the whole thing
        // to the box it is given, so the nominal unit size here never reaches the paper.
        return new SvgQRCode(data).GetGraphic(
            1,
            darkColorHex: "#111827",
            lightColorHex: "#FFFFFF",
            drawQuietZones: true,
            sizingMode: SvgQRCode.SizingMode.ViewBoxAttribute);
    }
}
