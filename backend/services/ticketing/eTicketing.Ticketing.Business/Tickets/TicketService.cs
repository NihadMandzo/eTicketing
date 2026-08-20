using System.Security.Claims;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Results;
using eTicketing.Ticketing.Business.Purchases;
using eTicketing.Ticketing.Business.Security;
using eTicketing.Ticketing.Data.Entities;
using eTicketing.Ticketing.Data.Repositories;

namespace eTicketing.Ticketing.Business.Tickets;

public class TicketService : ITicketService
{
    private readonly ITicketRepository _ticketRepository;

    public TicketService(ITicketRepository ticketRepository) => _ticketRepository = ticketRepository;

    public async Task<Result<PagedResult<TicketResponse>>> GetMineAsync(TicketQuery query, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var paged = await _ticketRepository.SearchByUserAsync(query, user.GetUserId(), ct);

        return Result<PagedResult<TicketResponse>>.Success(new PagedResult<TicketResponse>
        {
            Items = paged.Items.Select(ToResponse).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize,
        });
    }

    // Manual mapping (not Mapster) — SectorName/TicketTypeName come from navigations
    // (TicketRepository.SearchByUserAsync already .Includes Sector/TicketType), not flat fields.
    private static TicketResponse ToResponse(Ticket ticket) =>
        new(
            ticket.Id, ticket.OrderId, ticket.SectorId, ticket.Sector?.Name ?? "", ticket.ProductId,
            ticket.TicketTypeId, ticket.TicketType?.Name,
            ticket.Status, ticket.PricePaid, ticket.ValidDate, ticket.ValidFrom, ticket.ValidTo, ticket.CreatedAt);
}
