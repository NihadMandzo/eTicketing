using System.Text;
using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.PdfGeneration.Documents;
using eTicketing.PdfGeneration.Qr;
using FluentAssertions;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using UglyToad.PdfPig;

namespace eTicketing.PdfGeneration.Tests.Documents;

/// <summary>
/// Guards the ways this sheet can come out wrong without anything throwing.
///
/// Two of them were real during development: the sheet silently paginated into three pages when a
/// fixed-height element was a point too small for its own caption, and Manrope silently fell back
/// to QuestPDF's bundled Lato because Google Fonts' static instances declare their family as
/// "Manrope ExtraLight". Neither is visible to a test that only checks the bytes start with "%PDF",
/// so these assert page count, embedded font names, and the rendered text itself.
/// </summary>
public class TicketDocumentLayoutTests
{
    static TicketDocumentLayoutTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        TicketTheme.EnsureFontsRegistered();
    }

    [Theory]
    [InlineData(TicketingMode.SingleOccurrence)]
    [InlineData(TicketingMode.DailyEntry)]
    [InlineData(TicketingMode.RecurringReservation)]
    public void Render_ForEveryTicketingMode_ProducesExactlyOnePage(TicketingMode mode)
    {
        // The sheet is designed to be folded in three, so a second page means the fold marks have
        // moved and the terms panel is no longer on the back of the ticket.
        var bytes = Render(Document(mode));

        PageCount(bytes).Should().Be(1);
    }

    [Fact]
    public void Render_WithMaximumLengthText_StillProducesOnePage()
    {
        // Product and sector names are validated up to 200 characters and e-mail addresses are
        // effectively unbounded; unclamped, any of them pushes content out of a fixed-height panel.
        var longName = new string('Ž', 200);
        var document = Document(
            TicketingMode.SingleOccurrence,
            productName: longName,
            sectorName: new string('S', 200),
            ticketTypeName: new string('T', 120),
            userEmail: new string('e', 90) + "@primjer.ba");

        PageCount(Render(document)).Should().Be(1);
    }

    [Fact]
    public void Render_EmbedsTheDesignFonts_AndNeverFallsBackToASubstitute()
    {
        var pdf = Encoding.Latin1.GetString(Render(Document(TicketingMode.SingleOccurrence)));

        pdf.Should().Contain("Manrope").And.Contain("IBMPlexMono");
        // QuestPDF ships Lato and substitutes it silently for any family it cannot resolve; the
        // container's only installed family is Liberation. Either appearing means the embedded
        // faces were not matched and the ticket does not look like the design.
        pdf.Should().NotContain("Lato").And.NotContain("Liberation");
    }

    [Fact]
    public void Render_PutsTheFullTicketIdOnTheSheet_ForTheScannersManualFallback()
    {
        // Gate staff type this in when a camera won't read the code, so the complete GUID has to be
        // present — an abbreviated serial would quietly make manual entry impossible.
        var ticketId = Guid.NewGuid();

        var text = ExtractText(Render(Document(TicketingMode.SingleOccurrence, ticketId: ticketId)));

        // The stub is narrow enough that the id wraps, so compare without the line break.
        Collapse(text).Should().Contain(Collapse(ticketId.ToString().ToUpperInvariant()));
    }

    [Fact]
    public void Render_WritesEveryLabelInBosnian_WithDiacriticsIntact()
    {
        // The design's own labels are English; the project renders Bosnian. Diacritics are the part
        // that breaks silently — a font subset without latin-ext drops them without erroring.
        var text = Collapse(ExtractText(Render(Document(TicketingMode.SingleOccurrence))));

        foreach (var label in new[]
                 {
                     "DOGAĐAJ", "LOKACIJA", "SEKTOR", "CIJENA", "DATUMKUPOVINE", "VRIJEDI",
                     "SERIJSKIBROJ", "PODACIOULASKU", "NAČINULASKA", "TIPULAZNICE", "KUPAC",
                     "USLOVI,PRAVILAIPOVRATNOVCA", "VAŽENJE", "PRESAVIJTEOVDJE",
                 })
        {
            text.Should().Contain(label);
        }
    }

    [Theory]
    [InlineData(TicketingMode.SingleOccurrence, "JEDANULAZ")]
    [InlineData(TicketingMode.DailyEntry, "DNEVNIULAZ")]
    [InlineData(TicketingMode.RecurringReservation, "REZERVACIJA")]
    public void Render_BadgesTheEntryModeItWasBoughtUnder(TicketingMode mode, string expected)
    {
        Collapse(ExtractText(Render(Document(mode)))).Should().Contain(expected);
    }

    [Fact]
    public void Render_FormatsMoneyWithABosnianDecimalComma()
    {
        Collapse(ExtractText(Render(Document(TicketingMode.SingleOccurrence))))
            .Should().Contain("50,00KM").And.NotContain("50.00KM");
    }

    private static string ExtractText(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return string.Join(" ", document.GetPages().Select(p => p.Text));
    }

    /// <summary>Strips whitespace so assertions survive line wrapping and the letter-spacing the
    /// design applies to every label.</summary>
    private static string Collapse(string value) => new(value.Where(c => !char.IsWhiteSpace(c)).ToArray());

    private static byte[] Render(TicketDocument document)
    {
        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }

    /// <summary>Counts "/Type /Page" objects, discounting the "/Type /Pages" tree node.</summary>
    private static int PageCount(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        return text.Split("/Type /Page").Length - 1 - (text.Split("/Type /Pages").Length - 1);
    }

    private static TicketDocument Document(
        TicketingMode mode,
        string productName = "Ljetni Festival",
        string sectorName = "VIP",
        string? ticketTypeName = "Odrasli",
        string userEmail = "kupac@primjer.ba",
        Guid? ticketId = null)
    {
        var id = ticketId ?? Guid.NewGuid();
        var ticket = new PurchasedTicket(
            id,
            $"ETK1.{id:N}.signature",
            ticketTypeName,
            50m,
            mode == TicketingMode.DailyEntry ? new DateOnly(2026, 9, 1) : null,
            mode == TicketingMode.RecurringReservation ? new DateOnly(2026, 9, 1) : null,
            mode == TicketingMode.RecurringReservation ? new DateOnly(2026, 9, 30) : null);

        var order = new TicketPurchased(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), sectorName, mode,
            Guid.NewGuid(), userEmail, 50m, new DateTime(2026, 8, 24, 10, 8, 0, DateTimeKind.Utc),
            [ticket]);

        return new TicketDocument(
            order, ticket, productName,
            mode == TicketingMode.SingleOccurrence ? new DateTime(2026, 9, 1, 20, 0, 0, DateTimeKind.Utc) : null,
            "Sarajevo",
            QrCodeRenderer.Render(ticket.QrPayload),
            new TicketSupportInfo("podrska@ekarta.ba", "+387 33 555 120"));
    }
}
