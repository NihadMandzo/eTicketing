using System.Text;
using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.PdfGeneration.Documents;
using eTicketing.Shared.TicketPdf;
using FluentAssertions;
using MsOptions = Microsoft.Extensions.Options.Options;
using Microsoft.Extensions.Logging.Abstractions;
using QuestPDF.Infrastructure;

namespace eTicketing.PdfGeneration.Tests.Documents;

public class TicketPdfGeneratorTests
{
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
        // No client to mock any more: everything the sheet needs rides on the event. That is the
        // point of this constructor being one line — this service has no synchronous dependency on
        // eTicketing.Catalog left to stub, break, or time out.
        _sut = new TicketPdfGenerator(
            MsOptions.Create(new TicketSupportOptions { Email = "podrska@ekarta.ba", Phone = "+387 33 555 120" }),
            NullLogger<TicketPdfGenerator>.Instance);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsOnePdfPerTicket()
    {
        // One file per ticket, not one per order: each is handed to a different person at the gate.
        var order = Order(ticketCount: 3);

        var result = await _sut.GenerateAsync(order);

        result.Tickets.Should().HaveCount(3);
        result.Tickets.Select(t => t.TicketId).Should().BeEquivalentTo(order.Tickets.Select(t => t.TicketId));
    }

    [Fact]
    public async Task GenerateAsync_CarriesRealPdfBytesOnTheEvent()
    {
        // Nothing is uploaded anywhere — the bytes ARE the event payload, which is what
        // Notifications base64-encodes into the Brevo attachment.
        var result = await _sut.GenerateAsync(Order(ticketCount: 1));

        var bytes = result.Tickets.Single().Content;
        bytes.Should().NotBeEmpty();
        Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task GenerateAsync_TakesTheProductDetailsFromTheEventItself()
    {
        // The whole of this phase's change to this service, in one assertion: the name, date and
        // city on the outgoing email come off TicketPurchased, which eTicketing.Ticketing filled in
        // from its own ProductSnapshot at the moment of purchase.
        var result = await _sut.GenerateAsync(Order(ticketCount: 1));

        result.ProductName.Should().Be("Ljetni Festival");
        result.ProductDate.Should().Be(new DateTime(2026, 9, 1, 20, 0, 0, DateTimeKind.Utc));
        result.ProductCity.Should().Be(nameof(City.Sarajevo));
    }

    [Fact]
    public async Task GenerateAsync_GivesEachAttachmentADistinctReadableFileName()
    {
        // Three attachments all called "ulaznica.pdf" are indistinguishable in a mail client.
        var result = await _sut.GenerateAsync(Order(ticketCount: 3));

        var names = result.Tickets.Select(t => t.FileName).ToList();
        names.Should().OnlyHaveUniqueItems();
        names.Should().OnlyContain(n => n.StartsWith("ulaznica-") && n.EndsWith(".pdf"));
    }

    [Fact]
    public async Task GenerateAsync_CarriesOrderTotalsAndBuyerThroughToTheEvent()
    {
        var order = Order(ticketCount: 2);

        var result = await _sut.GenerateAsync(order);

        result.OrderId.Should().Be(order.OrderId);
        result.UserEmail.Should().Be(order.UserEmail);
        result.TotalPaid.Should().Be(order.TotalPaid);
    }

    [Fact]
    public async Task GenerateAsync_ForAnOrderPublishedBeforeTheProductFieldsExisted_StillRenders()
    {
        // The deploy-window case, and the reason those three fields are nullable. A TicketPurchased
        // published by the previous version is still on the queue and carries no product details.
        // Before this phase the missing-product branch dead-lettered the message; a buyer who has
        // already paid must get their ticket, with a generic heading if that is all we have.
        var order = Order(ticketCount: 1) with { ProductName = null, ProductDate = null, ProductCity = null };

        var result = await _sut.GenerateAsync(order);

        result.ProductName.Should().Be("Ulaznica");
        result.ProductCity.Should().BeEmpty();
        result.Tickets.Single().Content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_ForAProductNameThatIsBlank_FallsBackRatherThanPrintingNothing()
    {
        var order = Order(ticketCount: 1) with { ProductName = "   " };

        var result = await _sut.GenerateAsync(order);

        result.ProductName.Should().Be("Ulaznica");
    }

    [Theory]
    [InlineData(TicketingMode.SingleOccurrence)]
    [InlineData(TicketingMode.DailyEntry)]
    [InlineData(TicketingMode.RecurringReservation)]
    public async Task GenerateAsync_RendersEveryTicketingMode(TicketingMode mode)
    {
        // The validity line reads from different fields per mode — a null-ref in one branch would
        // only show up for that kind of product, long after the others shipped fine.
        var order = Order(ticketCount: 1, mode: mode) with
        {
            ProductDate = mode == TicketingMode.SingleOccurrence ? DateTime.UtcNow : null,
        };

        var result = await _sut.GenerateAsync(order);

        result.Tickets.Single().Content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_RendersAModeWithItsDatesMissing()
    {
        // Defensive: a DailyEntry ticket with no ValidDate shouldn't crash the worker, it should
        // print "Datum nije određen".
        var order = Order(ticketCount: 1, mode: TicketingMode.DailyEntry);
        order = order with
        {
            ProductDate = null,
            Tickets = [order.Tickets[0] with { ValidDate = null, ValidFrom = null, ValidTo = null }],
        };

        var act = async () => await _sut.GenerateAsync(order);

        await act.Should().NotThrowAsync();
    }

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
            Guid.NewGuid(), "buyer@example.com", 50 * ticketCount, DateTime.UtcNow, tickets,
            ProductName: "Ljetni Festival",
            ProductDate: new DateTime(2026, 9, 1, 20, 0, 0, DateTimeKind.Utc),
            ProductCity: City.Sarajevo);
    }
}
