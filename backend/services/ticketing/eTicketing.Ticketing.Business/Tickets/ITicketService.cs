using System.Security.Claims;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Results;
using eTicketing.Ticketing.Business.Purchases;

namespace eTicketing.Ticketing.Business.Tickets;

public interface ITicketService
{
    /// <summary>Customer — own tickets only (filtered by the caller's UserId from the JWT). No
    /// SuperAdmin/Admin "all tickets" override yet — deliberately deferred, see
    /// .claude/rules/01-domain.md.</summary>
    Task<Result<PagedResult<TicketResponse>>> GetMineAsync(TicketQuery query, ClaimsPrincipal user, CancellationToken ct = default);
}
