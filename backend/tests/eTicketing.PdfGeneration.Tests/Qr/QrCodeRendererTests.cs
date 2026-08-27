using System.Text.RegularExpressions;
using eTicketing.Shared.TicketPdf;
using FluentAssertions;
using QRCoder;

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

    // ------------------------------------------------------------------------- the vector variant
    //
    // PrintSheetDocument switched from Render to RenderSvg because a raster QR cost ~14 KB per
    // ticket in the finished PDF. These check that the cheaper form still encodes the *same code* —
    // a vector QR that drew even one module wrong would produce a sheet of tickets that all fail at
    // the gate, and nothing else in the pipeline would notice.

    [Fact]
    public void RenderSvg_DrawsExactlyTheModulesTheQrMatrixDefines()
    {
        var payload = $"ETK1.{Guid.NewGuid():N}.somesignature";

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);

        // QRCoder's matrix already includes its 4-module quiet zone on every side.
        var matrix = data.ModuleMatrix;
        var expectedDarkModules = matrix.Sum(row => row.Cast<bool>().Count(dark => dark));

        var svg = QrCodeRenderer.RenderSvg(payload);

        // QRCoder emits the dark modules as one path of run-length-merged horizontal segments,
        // each "M{x} {y}h{width}v1h-{width}z". Summing the widths counts the modules actually
        // painted, which is the thing that has to match the matrix exactly.
        var paintedModules = Regex
            .Matches(svg, @"M\d+ \d+h(?<width>\d+)v1h-\d+z")
            .Sum(match => int.Parse(match.Groups["width"].Value));

        paintedModules.Should().Be(expectedDarkModules);
    }

    [Fact]
    public void RenderSvg_SizesItsViewBoxToTheWholeSymbolIncludingTheQuietZone()
    {
        var payload = $"ETK1.{Guid.NewGuid():N}.somesignature";

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var moduleCount = data.ModuleMatrix.Count;

        var svg = QrCodeRenderer.RenderSvg(payload);

        // A viewBox is what lets QuestPDF scale the symbol to the stub without distorting it, and
        // the quiet zone has to be inside it — a scanner needs that clear margin to find the code.
        svg.Should().Contain($"viewBox=\"0 0 {moduleCount} {moduleCount}\"");
    }

    [Fact]
    public void RenderSvg_IsDeterministic_SoAReprintCarriesTheSameCode()
    {
        var payload = $"ETK1.{Guid.NewGuid():N}.somesignature";

        QrCodeRenderer.RenderSvg(payload).Should().Be(QrCodeRenderer.RenderSvg(payload));
    }

    [Fact]
    public void RenderSvg_ForDifferentPayloads_ProducesDifferentCodes()
    {
        var first = QrCodeRenderer.RenderSvg($"ETK1.{Guid.NewGuid():N}.sig");
        var second = QrCodeRenderer.RenderSvg($"ETK1.{Guid.NewGuid():N}.sig");

        first.Should().NotBe(second);
    }

    [Fact]
    public void RenderSvg_ForAFullLengthSignedPayload_StaysFarSmallerThanTheRaster()
    {
        // The size claim the switch was made for, pinned to the real payload length so it cannot
        // quietly regress back into a multi-kilobyte-per-ticket raster.
        var payload = $"ETK1.{Guid.NewGuid():N}.{new string('A', 22)}";

        var svg = QrCodeRenderer.RenderSvg(payload);
        var png = QrCodeRenderer.Render(payload);

        svg.Should().NotBeNullOrWhiteSpace();
        svg.Should().StartWith("<svg");
        svg.Length.Should().BeLessThan(png.Length * 40);
    }
}
