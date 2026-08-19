using System.Security.Claims;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Security;
using eTicketing.Ticketing.Data.Entities;
using eTicketing.Ticketing.Data.Repositories;
using Mapster;

namespace eTicketing.Ticketing.Business.Sectors;

public class SectorService : ISectorService
{
    // Buyers get a 5-minute decision window between holding a sector and confirming a purchase —
    // same figure specified for the (not-yet-built) Purchase flow in SPRINTS/SPRINT_2.md US-2.5.
    private static readonly TimeSpan HoldTtl = TimeSpan.FromMinutes(5);

    private readonly ISectorRepository _sectorRepository;
    private readonly ICatalogClient _catalogClient;
    private readonly ISectorCapacityLock _capacityLock;
    private readonly IUnitOfWork _unitOfWork;

    public SectorService(
        ISectorRepository sectorRepository,
        ICatalogClient catalogClient,
        ISectorCapacityLock capacityLock,
        IUnitOfWork unitOfWork)
    {
        _sectorRepository = sectorRepository;
        _catalogClient = catalogClient;
        _capacityLock = capacityLock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SectorPreviewResponse>> PreviewAsync(UpsertSectorRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var validation = await ValidateAsync(request, user, ct);
        if (validation.IsFailure)
            return Result<SectorPreviewResponse>.Failure(validation.Error);

        var product = validation.Value!;
        return Result<SectorPreviewResponse>.Success(new SectorPreviewResponse(
            request.ProductId, request.Name, request.Capacity, request.Price, product.TicketingMode, request.PeriodYear, request.PeriodMonth));
    }

    public async Task<Result<SectorResponse>> CreateAsync(UpsertSectorRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var validation = await ValidateAsync(request, user, ct);
        if (validation.IsFailure)
            return Result<SectorResponse>.Failure(validation.Error);

        var product = validation.Value!;
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            ProductId = request.ProductId,
            OrganizationId = product.OrganizationId,
            Name = request.Name,
            Capacity = request.Capacity,
            Price = request.Price,
            Status = PublishStatus.Draft,
            TicketingMode = product.TicketingMode,
            PeriodYear = request.PeriodYear,
            PeriodMonth = request.PeriodMonth,
        };

        await _sectorRepository.AddAsync(sector, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<SectorResponse>.Success(ToResponse(sector));
    }

    public async Task<Result<SectorResponse>> PublishAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var sector = await _sectorRepository.GetByIdAsync(id, ct);
        if (sector is null)
            return Result<SectorResponse>.Failure(Error.NotFound("sector.not_found", "Sektor nije pronađen."));

        var ownershipError = AuthorizeOwnership(user, sector.OrganizationId);
        if (ownershipError is not null)
            return Result<SectorResponse>.Failure(ownershipError);

        sector.Status = PublishStatus.Published;
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<SectorResponse>.Success(ToResponse(sector));
    }

    public async Task<Result<SectorResponse>> UpdateAsync(Guid id, UpsertSectorRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var sector = await _sectorRepository.GetByIdAsync(id, ct);
        if (sector is null)
            return Result<SectorResponse>.Failure(Error.NotFound("sector.not_found", "Sektor nije pronađen."));

        var ownershipError = AuthorizeOwnership(user, sector.OrganizationId);
        if (ownershipError is not null)
            return Result<SectorResponse>.Failure(ownershipError);

        // ProductId is immutable after creation — letting it change would silently re-parent
        // OrganizationId/TicketingMode and could invalidate outstanding holds' semantics once
        // ticket purchasing exists.
        if (request.ProductId != sector.ProductId)
            return Result<SectorResponse>.Failure(Error.Validation("sector.product_id_immutable", "Sektor se ne može premjestiti na drugi proizvod."));

        var validation = await ValidateAsync(request, user, ct);
        if (validation.IsFailure)
            return Result<SectorResponse>.Failure(validation.Error);

        var product = validation.Value!;

        // A Published sector stays Published after an edit — Status is deliberately untouched here.
        sector.Name = request.Name;
        sector.Capacity = request.Capacity;
        sector.Price = request.Price;
        sector.PeriodYear = request.PeriodYear;
        sector.PeriodMonth = request.PeriodMonth;
        sector.OrganizationId = product.OrganizationId;
        sector.TicketingMode = product.TicketingMode;

        await _unitOfWork.SaveChangesAsync(ct);

        return Result<SectorResponse>.Success(ToResponse(sector));
    }

    public async Task<Result> DeleteAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var sector = await _sectorRepository.GetByIdAsync(id, ct);
        if (sector is null)
            return Result.Failure(Error.NotFound("sector.not_found", "Sektor nije pronađen."));

        var ownershipError = AuthorizeOwnership(user, sector.OrganizationId);
        if (ownershipError is not null)
            return Result.Failure(ownershipError);

        _sectorRepository.Remove(sector);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<PagedResult<SectorResponse>>> GetPublishedAsync(SectorQuery query, CancellationToken ct = default)
    {
        // Simplification for this pass: filters by Sector.Status only, not the owning Product's
        // Status — an organizer is expected to only publish sectors for products they intend to
        // make public. Revisit once the Purchase flow needs a stricter guarantee.
        var paged = await _sectorRepository.SearchAsync(query, query.ProductId, null, PublishStatus.Published, ct);
        return ToPagedResult(paged);
    }

    public async Task<Result<PagedResult<SectorResponse>>> GetMineAsync(SectorQuery query, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var organizationId = user.GetOrganizationId();
        if (organizationId is null)
            return Result<PagedResult<SectorResponse>>.Failure(Error.Unauthorized("sector.no_organization", "Nalog nije vezan za organizaciju."));

        var paged = await _sectorRepository.SearchAsync(query, query.ProductId, organizationId, query.Status, ct);
        return ToPagedResult(paged);
    }

    public async Task<Result<PagedResult<SectorResponse>>> GetAllAsync(SectorQuery query, CancellationToken ct = default)
    {
        var paged = await _sectorRepository.SearchAsync(query, query.ProductId, null, query.Status, ct);
        return ToPagedResult(paged);
    }

    public async Task<Result<HoldSectorResponse>> HoldAsync(Guid sectorId, HoldSectorRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var sector = await _sectorRepository.GetByIdAsync(sectorId, ct);
        if (sector is null)
            return Result<HoldSectorResponse>.Failure(Error.NotFound("sector.not_found", "Sektor nije pronađen."));

        if (sector.Status != PublishStatus.Published)
            return Result<HoldSectorResponse>.Failure(Error.Validation("sector.not_published", "Sektor još nije objavljen."));

        if (request.Quantity < 1)
            return Result<HoldSectorResponse>.Failure(Error.Validation("sector.invalid_quantity", "Količina mora biti najmanje 1."));

        if (sector.TicketingMode == TicketingMode.DailyEntry)
        {
            if (request.Date is null)
                return Result<HoldSectorResponse>.Failure(Error.Validation("sector.date_required", "Datum je obavezan za ovaj tip sektora."));

            if (request.Date.Value.Year != sector.PeriodYear || request.Date.Value.Month != sector.PeriodMonth)
                return Result<HoldSectorResponse>.Failure(Error.Validation("sector.date_out_of_period", "Datum ne pripada periodu ovog sektora."));
        }
        else if (request.Date is not null)
        {
            return Result<HoldSectorResponse>.Failure(Error.Validation("sector.date_not_applicable", "Datum se ne unosi za ovaj tip sektora."));
        }

        var hold = await _capacityLock.TryHoldAsync(sector.Id, sector.Capacity, request.Quantity, request.Date, HoldTtl, ct);
        if (!hold.Success)
            return Result<HoldSectorResponse>.Failure(Error.Conflict("sector.no_capacity", "Nema dovoljno slobodnog kapaciteta."));

        return Result<HoldSectorResponse>.Success(new HoldSectorResponse(hold.HoldId!, hold.ExpiresAt!.Value));
    }

    /// <summary>Shared by PreviewAsync/CreateAsync/UpdateAsync so preview and the real write path
    /// always agree on the same validation message. Field-level rules (Name/Capacity/Price) ran
    /// via CreateSectorRequestValidator at the endpoint filter — this covers the cross-entity
    /// rules that need the owning Product's data: the product must exist and belong to the
    /// caller's organization (bypassed for PlatformStaff), and the Period/Capacity fields must
    /// match the product's Category.TicketingMode.</summary>
    private async Task<Result<CatalogProductResponse>> ValidateAsync(UpsertSectorRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var product = await _catalogClient.GetProductAsync(request.ProductId, ct);
        if (product is null)
            return Result<CatalogProductResponse>.Failure(Error.Validation("sector.product_not_found", "Odabrani proizvod ne postoji."));

        if (!user.IsPlatformStaff() && user.GetOrganizationId() != product.OrganizationId)
            return Result<CatalogProductResponse>.Failure(Error.Unauthorized("sector.forbidden", "Proizvod ne pripada vašoj organizaciji."));

        switch (product.TicketingMode)
        {
            case TicketingMode.SingleOccurrence:
                if (request.PeriodYear is not null || request.PeriodMonth is not null)
                    return Result<CatalogProductResponse>.Failure(Error.Validation("sector.period_not_applicable", "Period se ne unosi za ovaj tip proizvoda."));
                break;

            case TicketingMode.DailyEntry:
                if (request.PeriodYear is null || request.PeriodMonth is null)
                    return Result<CatalogProductResponse>.Failure(Error.Validation("sector.period_required", "Godina i mjesec su obavezni za ovaj tip proizvoda."));
                if (request.PeriodMonth is < 1 or > 12)
                    return Result<CatalogProductResponse>.Failure(Error.Validation("sector.period_invalid_month", "Mjesec mora biti između 1 i 12."));
                break;

            case TicketingMode.RecurringReservation:
                if (request.PeriodYear is not null || request.PeriodMonth is not null)
                    return Result<CatalogProductResponse>.Failure(Error.Validation("sector.period_not_applicable", "Period se ne unosi za ovaj tip proizvoda."));
                if (request.Capacity != 1)
                    return Result<CatalogProductResponse>.Failure(Error.Validation("sector.capacity_must_be_one", "Kapacitet mora biti tačno 1 za rezervaciju parking mjesta."));
                break;
        }

        return Result<CatalogProductResponse>.Success(product);
    }

    /// <summary>PlatformStaff bypasses ownership entirely; everyone else must own the sector's organization.</summary>
    private static Error? AuthorizeOwnership(ClaimsPrincipal user, Guid organizationId)
    {
        if (user.IsPlatformStaff())
            return null;

        if (user.GetOrganizationId() != organizationId)
            return Error.Unauthorized("sector.forbidden", "Sektor ne pripada vašoj organizaciji.");

        return null;
    }

    private static Result<PagedResult<SectorResponse>> ToPagedResult(PagedResult<Sector> paged) =>
        Result<PagedResult<SectorResponse>>.Success(new PagedResult<SectorResponse>
        {
            Items = paged.Items.Select(ToResponse).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize,
        });

    private static SectorResponse ToResponse(Sector sector) => sector.Adapt<SectorResponse>();
}
