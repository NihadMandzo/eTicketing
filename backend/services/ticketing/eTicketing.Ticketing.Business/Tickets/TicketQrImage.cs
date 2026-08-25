using QRCoder;

namespace eTicketing.Ticketing.Business.Tickets;

/// <summary>
/// Renders a QR payload straight into a <c>data:image/png;base64,...</c> URI that both frontends
/// drop into an &lt;img&gt;/Image.memory unchanged.
///
/// Rendering server-side is a deliberate choice over shipping a QR library to Angular AND Flutter:
/// one implementation, one place to fix, and neither client gains a dependency just to draw a
/// square. Roughly 1.5 KB per ticket, so a default 20-row page of GET /tickets/mine costs ~30 KB.
///
/// PngByteQRCode (not the System.Drawing-backed QRCode renderer) because this runs in a Linux
/// container with no libgdiplus.
/// </summary>
public static class TicketQrImage
{
    private const int PixelsPerModule = 8;

    public static string ToDataUri(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(PixelsPerModule);
        return $"data:image/png;base64,{Convert.ToBase64String(png)}";
    }

    /// <summary>Raw PNG bytes — used by tests; the PDF worker has its own renderer since it never
    /// references this assembly.</summary>
    public static byte[] ToPng(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(PixelsPerModule);
    }
}
