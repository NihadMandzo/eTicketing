using System.Security.Claims;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Results;
using eTicketing.Ticketing.Business.Purchases;
using eTicketing.Contracts.Security;
using eTicketing.Ticketing.Business.Security;
using eTicketing.Ticketing.Data.Repositories;

namespace eTicketing.Ticketing.Business.Tickets;

public class TicketService : ITicketService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly TicketResponseFactory _responseFactory;

    public TicketService(ITicketRepository ticketRepository, TicketResponseFactory responseFactory)
    {
        _ticketRepository = ticketRepository;
        _responseFactory = responseFactory;
    }

    public async Task<Result<PagedResult<TicketResponse>>> GetMineAsync(TicketQuery query, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var paged = await _ticketRepository.SearchByUserAsync(query, user.GetUserId(), ct);

        return Result<PagedResult<TicketResponse>>.Success(new PagedResult<TicketResponse>
        {
            // TicketRepository.SearchByUserAsync already .Includes Sector/TicketType, so the
            // factory's navigation-based overload resolves SectorName/TicketTypeName without a
            // per-row query.
            Items = paged.Items.Select(_responseFactory.Create).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize,
        });
    }
}
