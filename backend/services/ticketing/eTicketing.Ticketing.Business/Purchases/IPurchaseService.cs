using System.Security.Claims;
using eTicketing.Contracts.Results;

namespace eTicketing.Ticketing.Business.Purchases;

public interface IPurchaseService
{
    /// <summary>Orchestrates the synchronous purchase critical path: resolves the hold's actual
    /// reservation from Redis (never trusting client-supplied Sector/quantity claims), validates
    /// the request's line items against it and against the Sector's TicketTypes, charges via
    /// eTicketing.Payment (mocked), confirms the hold, mints one Ticket per admission unit, and
    /// publishes TicketPurchased per Ticket. See .claude/rules/01-domain.md's purchase critical
    /// path and PurchaseService's own class doc for the full step-by-step.</summary>
    Task<Result<PurchaseResponse>> PurchaseAsync(PurchaseRequest request, ClaimsPrincipal user, CancellationToken ct = default);
}
