using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.Integration;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using eTicketing.Ticketing.Business.Tickets;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;

namespace eTicketing.Ticketing.Business.Tests.Integration;

/// <summary>Covers the eTicketing.PdfGeneration → Ticketing half of SPRINT_4 T-4.2.4: the return
/// event, not an HTTP callback. Everything here has to survive at-least-once delivery, so
/// idempotence gets as much attention as the happy path.</summary>
public class TicketPdfCompletionServiceTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();
    private readonly ITicketPdfCompletionService _sut;
    private readonly Guid _productId = Guid.NewGuid();

    public TicketPdfCompletionServiceTests() => _sut = _fixture.CreateTicketPdfCompletionService();

    [Fact]
    public async Task ApplyAsync_MovesEveryTicketInTheOrderToReady()
    {
        var orderId = Guid.NewGuid();
        var first = await SeedTicketAsync(orderId);
        var second = await SeedTicketAsync(orderId);

        await _sut.ApplyAsync(Event(orderId, first.Id, second.Id));

        var tickets = _fixture.DbContext.Tickets.Where(t => t.OrderId == orderId).ToList();
        tickets.Should().HaveCount(2);
        tickets.Should().OnlyContain(t => t.Status == TicketStatus.Ready);
    }

    [Fact]
    public async Task ApplyAsync_AppliedTwice_IsANoOp()
    {
        var orderId = Guid.NewGuid();
        var ticket = await SeedTicketAsync(orderId);
        var message = Event(orderId, ticket.Id);

        await _sut.ApplyAsync(message);
        await _sut.ApplyAsync(message);

        var reloaded = await _fixture.DbContext.Tickets.FindAsync(ticket.Id);
        reloaded!.Status.Should().Be(TicketStatus.Ready);
    }

    [Fact]
    public async Task ApplyAsync_ForATicketAlreadyScannedAtTheGate_KeepsUsedStatus()
    {
        // Possible for a same-day at-the-door purchase: the buyer walks in before the PDF worker
        // catches up. Used is terminal — resurrecting it to Ready would make the ticket admittable
        // again, which is exactly what validation is supposed to prevent.
        var orderId = Guid.NewGuid();
        var ticket = await SeedTicketAsync(orderId, TicketStatus.Used);

        await _sut.ApplyAsync(Event(orderId, ticket.Id));

        var reloaded = await _fixture.DbContext.Tickets.FindAsync(ticket.Id);
        reloaded!.Status.Should().Be(TicketStatus.Used);
    }

    [Fact]
    public async Task ApplyAsync_ForAnUnknownTicketId_SkipsItInsteadOfThrowing()
    {
        // Dead-lettering the whole order because one ticket was deleted helps nobody.
        var orderId = Guid.NewGuid();
        var known = await SeedTicketAsync(orderId);

        var act = async () => await _sut.ApplyAsync(Event(orderId, known.Id, Guid.NewGuid()));

        await act.Should().NotThrowAsync();
        (await _fixture.DbContext.Tickets.FindAsync(known.Id))!.Status.Should().Be(TicketStatus.Ready);
    }

    [Fact]
    public async Task ApplyAsync_ForAnEventNamingTheSameTicketTwice_DoesNotThrow()
    {
        var orderId = Guid.NewGuid();
        var ticket = await SeedTicketAsync(orderId);

        var act = async () => await _sut.ApplyAsync(Event(orderId, ticket.Id, ticket.Id));

        await act.Should().NotThrowAsync();
        (await _fixture.DbContext.Tickets.FindAsync(ticket.Id))!.Status.Should().Be(TicketStatus.Ready);
    }

    [Fact]
    public async Task ApplyAsync_WithNoTicketsAtAll_DoesNothing()
    {
        var act = async () => await _sut.ApplyAsync(Event(Guid.NewGuid()));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ApplyAsync_LeavesTicketsFromOtherOrdersUntouched()
    {
        var orderId = Guid.NewGuid();
        var mine = await SeedTicketAsync(orderId);
        var other = await SeedTicketAsync(Guid.NewGuid());

        await _sut.ApplyAsync(Event(orderId, mine.Id));

        (await _fixture.DbContext.Tickets.FindAsync(other.Id))!.Status.Should().Be(TicketStatus.Confirmed);
    }

    private static TicketPdfReady Event(Guid orderId, params Guid[] ticketIds) =>
        new(orderId, Guid.NewGuid(), Guid.NewGuid(), "buyer@example.com",
            "Test proizvod", null, "Sarajevo", 50 * ticketIds.Length,
            ticketIds.Select(id => new TicketPdf(
                id, "%PDF-1.4 fake"u8.ToArray(),
                $"ulaznica-{id.ToString("N")[..8]}.pdf", "VIP", null, 50)).ToList());

    private async Task<Ticket> SeedTicketAsync(Guid orderId, TicketStatus status = TicketStatus.Confirmed)
    {
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            ProductId = _productId,
            OrganizationId = Guid.NewGuid(),
            Name = "VIP",
            Capacity = 100,
            Price = 50,
            Status = PublishStatus.Published,
            TicketingMode = TicketingMode.SingleOccurrence,
        };
        _fixture.DbContext.Sectors.Add(sector);

        var ticket = Ticket.ForSingleOccurrence(
            sector.Id, null, orderId, _productId, Guid.NewGuid(), "buyer@example.com", 50);
        ticket.Status = status;

        _fixture.DbContext.Tickets.Add(ticket);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
        return ticket;
    }

    public void Dispose() => _fixture.Dispose();
}
