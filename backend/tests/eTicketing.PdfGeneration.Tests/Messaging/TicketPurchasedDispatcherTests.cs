using System.Text;
using System.Text.Json;
using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.PdfGeneration.Documents;
using eTicketing.PdfGeneration.Messaging;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace eTicketing.PdfGeneration.Tests.Messaging;

/// <summary>
/// The failure taxonomy matters as much as the happy path here: a PoisonMessageException parks the
/// message in the dead-letter queue forever, while anything else climbs the retry ladder. Getting
/// that backwards either retries a permanently-broken message every 30 minutes for good, or throws
/// away a buyer's ticket over a momentary Azure blip.
/// </summary>
public class TicketPurchasedDispatcherTests
{
    private readonly Mock<ITicketPdfGenerator> _generator = new();
    private readonly Mock<IEventPublisher> _eventPublisher = new();
    private readonly TicketPurchasedDispatcher _sut;

    public TicketPurchasedDispatcherTests() =>
        _sut = new TicketPurchasedDispatcher(
            _generator.Object, _eventPublisher.Object, NullLogger<TicketPurchasedDispatcher>.Instance);

    [Fact]
    public async Task DispatchAsync_OnSuccess_PublishesTicketPdfReady()
    {
        var order = Order(ticketCount: 2);
        var ready = ReadyFor(order);
        _generator.Setup(g => g.GenerateAsync(It.IsAny<TicketPurchased>(), It.IsAny<CancellationToken>())).ReturnsAsync(ready);

        await _sut.DispatchAsync(EventNames.TicketPurchased, Serialize(order));

        _eventPublisher.Verify(
            p => p.PublishAsync(EventNames.TicketPdfReady, ready, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ForAnUnknownRoutingKey_ThrowsPoisonMessageException()
    {
        var act = async () => await _sut.DispatchAsync("some.unknown.key", Encoding.UTF8.GetBytes("{}"));

        await act.Should().ThrowAsync<PoisonMessageException>();
        _eventPublisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DispatchAsync_ForMalformedJson_ThrowsPoisonMessageException()
    {
        var act = async () => await _sut.DispatchAsync(EventNames.TicketPurchased, Encoding.UTF8.GetBytes("not valid json"));

        await act.Should().ThrowAsync<PoisonMessageException>();
        _eventPublisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DispatchAsync_ForAnOrderWithNoTickets_ThrowsPoisonMessageException()
    {
        // Retrying can't conjure tickets into an order that never had any.
        var act = async () => await _sut.DispatchAsync(EventNames.TicketPurchased, Serialize(Order(ticketCount: 0)));

        await act.Should().ThrowAsync<PoisonMessageException>();
    }

    [Fact]
    public async Task DispatchAsync_WhenTheProductNoLongerExists_ThrowsPoisonMessageException()
    {
        // A deleted product never comes back — retrying forever would just keep one message
        // circulating.
        _generator
            .Setup(g => g.GenerateAsync(It.IsAny<TicketPurchased>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TicketPdfReady?)null);

        var act = async () => await _sut.DispatchAsync(EventNames.TicketPurchased, Serialize(Order(ticketCount: 1)));

        await act.Should().ThrowAsync<PoisonMessageException>();
        _eventPublisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DispatchAsync_WhenGenerationFailsTransiently_LetsTheOriginalExceptionThroughForRetry()
    {
        // NOT a PoisonMessageException: Azure being briefly unreachable must climb the retry
        // ladder, because the buyer's confirmation email is chained behind this event.
        _generator
            .Setup(g => g.GenerateAsync(It.IsAny<TicketPurchased>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Blob storage nedostupan."));

        var act = async () => await _sut.DispatchAsync(EventNames.TicketPurchased, Serialize(Order(ticketCount: 1)));

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task DispatchAsync_WhenPublishingFails_LetsTheExceptionThroughForRetry()
    {
        // The PDFs are already in blob storage but nobody has been told — that must not be
        // swallowed, or the buyer silently gets no email at all.
        _generator
            .Setup(g => g.GenerateAsync(It.IsAny<TicketPurchased>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReadyFor(Order(ticketCount: 1)));
        _eventPublisher
            .Setup(p => p.PublishAsync(EventNames.TicketPdfReady, It.IsAny<TicketPdfReady>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("RabbitMQ nedostupan."));

        var act = async () => await _sut.DispatchAsync(EventNames.TicketPurchased, Serialize(Order(ticketCount: 1)));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static ReadOnlyMemory<byte> Serialize<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value);

    private static TicketPurchased Order(int ticketCount)
    {
        var tickets = Enumerable.Range(0, ticketCount).Select(_ =>
        {
            var ticketId = Guid.NewGuid();
            return new PurchasedTicket(ticketId, $"ETK1.{ticketId:N}.sig", null, 50, null, null, null);
        }).ToList();

        return new TicketPurchased(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "VIP", TicketingMode.SingleOccurrence,
            Guid.NewGuid(), "buyer@example.com", 50 * ticketCount, DateTime.UtcNow, tickets);
    }

    private static TicketPdfReady ReadyFor(TicketPurchased order) =>
        new(order.OrderId, order.ProductId, order.UserId, order.UserEmail,
            "Ljetni Festival", DateTime.UtcNow, "Sarajevo", order.TotalPaid,
            order.Tickets.Select(t => new TicketPdf(
                t.TicketId, "%PDF-1.4 fake"u8.ToArray(),
                "ulaznica.pdf", "VIP", null, 50)).ToList());
}
