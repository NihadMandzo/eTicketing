using System.Security.Claims;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Results;

namespace eTicketing.Ticketing.Business.Sectors;

public interface ISectorService
{
    /// <summary>Stateless — validates the request without writing to the database, using the
    /// same ValidateAsync() rules as CreateAsync.</summary>
    Task<Result<SectorPreviewResponse>> PreviewAsync(UpsertSectorRequest request, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Creates as Status=Draft. Calls ICatalogClient to confirm the caller's organization
    /// owns ProductId and to copy the product's Category.TicketingMode/OrganizationId onto the
    /// new Sector.</summary>
    Task<Result<SectorResponse>> CreateAsync(UpsertSectorRequest request, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Draft → Published. Ownership-checked (own organization or PlatformStaff).</summary>
    Task<Result<SectorResponse>> PublishAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Saves edits regardless of status. Ownership-checked.</summary>
    Task<Result<SectorResponse>> UpdateAsync(Guid id, UpsertSectorRequest request, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Ownership-checked.</summary>
    Task<Result> DeleteAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Public — Status=Published sectors only, for a given product.</summary>
    Task<Result<PagedResult<SectorResponse>>> GetPublishedAsync(SectorQuery query, CancellationToken ct = default);

    /// <summary>Organizer — own organization's sectors, any status.</summary>
    Task<Result<PagedResult<SectorResponse>>> GetMineAsync(SectorQuery query, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>PlatformStaff — every organization's sectors, any status.</summary>
    Task<Result<PagedResult<SectorResponse>>> GetAllAsync(SectorQuery query, CancellationToken ct = default);

    /// <summary>Atomic capacity hold via ISectorCapacityLock, TTL 5 minutes. For DailyEntry
    /// sectors, request.Date is required and must fall within the sector's
    /// PeriodYear/PeriodMonth. Any authenticated user may hold (RequireAuthorization() with no
    /// specific role at the endpoint) — the ownership concept doesn't apply to buyers.</summary>
    Task<Result<HoldSectorResponse>> HoldAsync(Guid sectorId, HoldSectorRequest request, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Best-effort early release of a still-live hold — e.g. the buyer switched to a
    /// different spot/date before completing checkout, or one hold in a multi-hold cart failed so
    /// the others must be given back rather than left to expire on their own TTL. A thin wrapper
    /// over ISectorCapacityLock.ReleaseAsync: always succeeds even for an unknown/already-expired
    /// holdId (same no-op semantics as ReleaseAsync itself), since the caller only ever wants
    /// "make sure this hold isn't holding capacity any more", never confirmation it existed.</summary>
    Task<Result> ReleaseHoldAsync(string holdId, CancellationToken ct = default);
}
