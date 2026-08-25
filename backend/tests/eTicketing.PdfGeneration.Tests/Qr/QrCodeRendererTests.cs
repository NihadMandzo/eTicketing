using eTicketing.Shared.TicketPdf;
using FluentAssertions;

namespace eTicketing.PdfGeneration.Tests.Qr;

public class QrCodeRendererTests
{
    /// <summary>PNG signature: 0x89 "PNG" CR LF SUB LF.</summary>
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void Render_ProducesRealPngBytes()
    {
        var png = QrCodeRenderer.Render($"ETK1.{Guid.NewGuid():N}.somesignature");

        png.Should().NotBeEmpty();
        png.Take(PngMagic.Length).Should().Equal(PngMagic);
    }

    [Fact]
    public void Render_IsDeterministic_SoAReissuedPdfCarriesTheSameCode()
    {
        var payload = $"ETK1.{Guid.NewGuid():N}.somesignature";

        QrCodeRenderer.Render(payload).Should().Equal(QrCodeRenderer.Render(payload));
    }

    [Fact]
    public void Render_ForDifferentPayloads_ProducesDifferentImages()
    {
        var first = QrCodeRenderer.Render($"ETK1.{Guid.NewGuid():N}.sig");
        var second = QrCodeRenderer.Render($"ETK1.{Guid.NewGuid():N}.sig");

        first.Should().NotEqual(second);
    }

    [Fact]
    public void Render_ForAFullLengthSignedPayload_Succeeds()
    {
        // The real thing, at its real length — a QR that can't encode the production payload would
        // otherwise only be discovered at the gate.
        var payload = $"ETK1.{Guid.NewGuid():N}.{new string('A', 22)}";

        QrCodeRenderer.Render(payload).Should().NotBeEmpty();
    }
}
