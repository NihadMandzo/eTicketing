using System.Security.Claims;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using eTicketing.Ticketing.Business.Tickets;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;

namespace eTicketing.Ticketing.Business.Tests.Tickets;

public class TicketServiceTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();
    private readonly ITicketService _sut;

    public TicketServiceTests() => _sut = _fixture.CreateTicketService();

    private static ClaimsPrincipal BuildCaller(Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, "User"),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    // Ticket.SectorId is a real FK (OnDelete Restrict) — needs an actual Sector row to point at.
    // Goes straight through the repository rather than SectorService since these tests don't care
    // about Sector business rules, only that the FK is satisfiable.
    private async Task<Sector> SeedSectorAsync()
    {
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Name = "VIP",
            Capacity = 100,
            Price = 50,
            Status = PublishStatus.Published,
            TicketingMode = TicketingMode.SingleOccurrence,
        };
        await _fixture.SectorRepository.AddAsync(sector);
        await _fixture.UnitOfWork.SaveChangesAsync();
        return sector;
    }

    private async Task<Ticket> SeedTicketAsync(Guid userId, Guid sectorId, Guid productId)
    {
        var ticket = Ticket.ForSingleOccurrence(sectorId, null, Guid.NewGuid(), productId, userId, "buyer@example.com", 50);
        await _fixture.TicketRepository.AddAsync(ticket);
        await _fixture.UnitOfWork.SaveChangesAsync();
        return ticket;
    }

    [Fact]
    public async Task GetMineAsync_ReturnsOnlyCallersOwnTickets()
    {
        var sector = await SeedSectorAsync();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        await SeedTicketAsync(userA, sector.Id, sector.ProductId);
        await SeedTicketAsync(userA, sector.Id, sector.ProductId);
        await SeedTicketAsync(userB, sector.Id, sector.ProductId);

        var result = await _sut.GetMineAsync(new TicketQuery(), BuildCaller(userA));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items.Should().OnlyContain(t => t.SectorName == "VIP");
    }

    [Fact]
    public async Task GetMineAsync_EmptyForUserWithNoPurchases_ReturnsEmptyPagedResult()
    {
        var sector = await SeedSectorAsync();
        await SeedTicketAsync(Guid.NewGuid(), sector.Id, sector.ProductId);

        var result = await _sut.GetMineAsync(new TicketQuery(), BuildCaller(Guid.NewGuid()));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    public void Dispose() => _fixture.Dispose();
}
