using System.Security.Claims;
using eTicketing.Contracts.Results;

namespace eTicketing.Ticketing.Business.Tickets;

public interface ITicketValidationService
{
    /// <summary>Organizer — products of the caller's own organization with tickets admitting entry
    /// TODAY. PlatformStaff sees every organization's.</summary>
    Task<Result<List<ValidationProductResponse>>> GetProductsForTodayAsync(ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Scans one code against one product and, if everything checks out, burns the ticket
    /// (Status → Used) so it can never be admitted twice.</summary>
    Task<Result<TicketValidationResponse>> ValidateAsync(ValidateTicketRequest request, ClaimsPrincipal user, CancellationToken ct = default);
}
