using System.Security.Claims;
using eTicketing.Contracts.Results;

namespace eTicketing.Ticketing.Business.Sectors;

public interface ITicketTypeService
{
    /// <summary>Public — no status gating (mirrors SectorService.GetPublishedAsync's own
    /// simplification note); returns NotFound only if the Sector itself doesn't exist.</summary>
    Task<Result<List<TicketTypeResponse>>> GetBySectorAsync(Guid sectorId, CancellationToken ct = default);

    /// <summary>Ownership-checked (own organization or PlatformStaff) against the parent Sector.</summary>
    Task<Result<TicketTypeResponse>> CreateAsync(Guid sectorId, UpsertTicketTypeRequest request, ClaimsPrincipal user, CancellationToken ct = default);

    Task<Result<TicketTypeResponse>> UpdateAsync(Guid sectorId, Guid id, UpsertTicketTypeRequest request, ClaimsPrincipal user, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid sectorId, Guid id, ClaimsPrincipal user, CancellationToken ct = default);
}
