using System.Security.Claims;
using eTicketing.Contracts.Results;

namespace eTicketing.Ticketing.Business.Sectors;

public interface ITicketTypeService
{
    /// <summary>Anonymous route, but not an unconditionally public read: a Draft sector's tiers are
    /// unannounced names and prices, and this endpoint used to hand them to anyone who knew the
    /// sector id. Only the sector's own organization and PlatformStaff can read a Draft one — which
    /// they must, since the desktop back-office adds tiers to a sector before publishing it. For
    /// everyone else a Draft sector answers exactly as a non-existent one does, rather than with a
    /// 403 that would confirm it exists.</summary>
    Task<Result<List<TicketTypeResponse>>> GetBySectorAsync(Guid sectorId, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Ownership-checked (own organization or PlatformStaff) against the parent Sector.</summary>
    Task<Result<TicketTypeResponse>> CreateAsync(Guid sectorId, UpsertTicketTypeRequest request, ClaimsPrincipal user, CancellationToken ct = default);

    Task<Result<TicketTypeResponse>> UpdateAsync(Guid sectorId, Guid id, UpsertTicketTypeRequest request, ClaimsPrincipal user, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid sectorId, Guid id, ClaimsPrincipal user, CancellationToken ct = default);
}
