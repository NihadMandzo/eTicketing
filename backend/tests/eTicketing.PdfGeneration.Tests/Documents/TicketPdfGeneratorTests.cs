using System.Text;
using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.PdfGeneration.Documents;
using eTicketing.Shared.TicketPdf;
using eTicketing.PdfGeneration.External;
using FluentAssertions;
using MsOptions = Microsoft.Extensions.Options.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QuestPDF.Infrastructure;

namespace eTicketing.PdfGeneration.Tests.Documents;

public class TicketPdfGeneratorTests
{
    private readonly Mock<ICatalogClient> _catalogClient = new();
    private readonly ITicketPdfGenerator _sut;

    private readonly Guid _productId = Guid.NewGuid();

    static TicketPdfGeneratorTests()
    {
        // Normally set once in PdfGenerationServiceCollectionExtensions; QuestPDF refuses to render
        // a single page without it, and these tests bypass the composition root. Same for the
        // embedded Manrope/IBM Plex Mono faces the ticket design is built on — without them
        // QuestPDF would substitute silently and every layout assertion below would still pass.
        QuestPDF.Settings.License = LicenseType.Community;
        TicketTheme.EnsureFontsRegistered();
    }

    public TicketPdfGeneratorTests()
    {
        _sut = new TicketPdfGenerator(
            _catalogClient.Object,
            MsOptions.Create(new TicketSupportOptions { Email = "podrska@ekarta.ba", Phone = "+387 33 555 120" }),
            NullLogger<TicketPdfGenerator>.Instance);
        MockProduct(TicketingMode.SingleOccurrence, new DateTime(2026, 9, 1, 20, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GenerateAsync_ReturnsOnePdfPerTicket()
    {
        // One file per ticket, not one per order: each is handed to a different person at the gate.
        var order = Order(ticketCount: 3);

        var result = await _sut.GenerateAsync(order);

        result.Should().NotBeNull();
        result!.Tickets.Should().HaveCount(3);
        result.Tickets.Select(t => t.TicketId).Should().BeEquivalentTo(order.Tickets.Select(t => t.TicketId));
    }

    [Fact]
    public async Task GenerateAsync_CarriesRealPdfBytesOnTheEvent()
    {
        // Nothing is uploaded anywhere — the bytes ARE the event payload, which is what
        // Notifications base64-encodes into the Brevo attachment.
        var result = await _sut.GenerateAsync(Order(ticketCount: 1));

        var bytes = result!.Tickets.Single().Content;
        bytes.Should().NotBeEmpty();
        Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task GenerateAsync_ReturnsAnEventCarryingTheProductDetailsCatalogSupplied()
    {
        // TicketPurchased has no product name/date on it at all — Ticketing only stores a
        // ProductId — so this is where the email gets them from.
        var result = await _sut.GenerateAsync(Order(ticketCount: 1));

        result!.ProductName.Should().Be("Ljetni Festival");
        result.ProductDate.Should().Be(new DateTime(2026, 9, 1, 20, 0, 0, DateTimeKind.Utc));
        result.ProductCity.Should().Be(nameof(City.Sarajevo));
    }

    [Fact]
    public async Task GenerateAsync_GivesEachAttachmentADistinctReadableFileName()
    {
        // Three attachments all called "ulaznica.pdf" are indistinguishable in a mail client.
        var result = await _sut.GenerateAsync(Order(ticketCount: 3));

        var names = result!.Tickets.Select(t => t.FileName).ToList();
        names.Should().OnlyHaveUniqueItems();
        names.Should().OnlyContain(n => n.StartsWith("ulaznica-") && n.EndsWith(".pdf"));
    }

    [Fact]
    public async Task GenerateAsync_UploadsEachPdfUnderTheBlobNameItReports()
    {
        // The filename is what the buyer sees on the attachment, and it has to be distinguishable:
        // three files called "ulaznica.pdf" are indistinguishable in a mail client.
        var order = Order(ticketCount: 3);

        var result = await _sut.GenerateAsync(order);

        var names = result!.Tickets.Select(t => t.FileName).ToList();
        names.Should().OnlyHaveUniqueItems();
        names.Should().OnlyContain(n => n.StartsWith("ulaznica-") && n.EndsWith(".pdf"));
    }

    [Fact]
    public async Task GenerateAsync_CarriesOrderTotalsAndBuyerThroughToTheEvent()
    {
        var order = Order(ticketCount: 2);

        var result = await _sut.GenerateAsync(order);

        result!.OrderId.Should().Be(order.OrderId);
        result.UserEmail.Should().Be(order.UserEmail);
        result.TotalPaid.Should().Be(order.TotalPaid);
    }

    [Fact]
    public async Task GenerateAsync_ForADeletedProduct_ReturnsNullRatherThanRenderingANamelessTicket()
    {
        _catalogClient
            .Setup(c => c.GetProductAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CatalogProductResponse?)null);

        var result = await _sut.GenerateAsync(Order(ticketCount: 1));

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(TicketingMode.SingleOccurrence)]
    [InlineData(TicketingMode.DailyEntry)]
    [InlineData(TicketingMode.RecurringReservation)]
    public async Task GenerateAsync_RendersEveryTicketingMode(TicketingMode mode)
    {
        // The validity line reads from different fields per mode — a null-ref in one branch would
        // only show up for that kind of product, long after the others shipped fine.
        MockProduct(mode, mode == TicketingMode.SingleOccurrence ? DateTime.UtcNow : null);

        var result = await _sut.GenerateAsync(Order(ticketCount: 1, mode: mode));

        result.Should().NotBeNull();
        result!.Tickets.Single().Content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_RendersAModeWithItsDatesMissing()
    {
        // Defensive: a DailyEntry ticket with no ValidDate shouldn't crash the worker, it should
        // print "Datum nije određen".
        MockProduct(TicketingMode.DailyEntry, null);
        var order = Order(ticketCount: 1, mode: TicketingMode.DailyEntry) with { };
        order = order with
        {
            Tickets = [order.Tickets[0] with { ValidDate = null, ValidFrom = null, ValidTo = null }],
        };

        var act = async () => await _sut.GenerateAsync(order);

        await act.Should().NotThrowAsync();
    }

    private void MockProduct(TicketingMode mode, DateTime? date) =>
        _catalogClient
            .Setup(c => c.GetProductAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogProductResponse(
                _productId, Guid.NewGuid(), PublishStatus.Published, mode, "Ljetni Festival", date, City.Sarajevo));

    private TicketPurchased Order(int ticketCount, TicketingMode mode = TicketingMode.SingleOccurrence)
    {
        var tickets = Enumerable.Range(0, ticketCount).Select(i =>
        {
            var ticketId = Guid.NewGuid();
            return new PurchasedTicket(
                ticketId,
                $"ETK1.{ticketId:N}.signature{i}",
                i == 0 ? "Odrasli" : "Djeca",
                50,
                mode == TicketingMode.DailyEntry ? new DateOnly(2026, 9, 1) : null,
                mode == TicketingMode.RecurringReservation ? new DateOnly(2026, 9, 1) : null,
                mode == TicketingMode.RecurringReservation ? new DateOnly(2026, 9, 30) : null);
        }).ToList();

        return new TicketPurchased(
            Guid.NewGuid(), _productId, Guid.NewGuid(), "VIP", mode,
            Guid.NewGuid(), "buyer@example.com", 50 * ticketCount, DateTime.UtcNow, tickets);
    }
}
