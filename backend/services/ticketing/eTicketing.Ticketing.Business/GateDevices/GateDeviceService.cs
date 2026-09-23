using System.Security.Claims;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Ticketing.Business.External;
using eTicketing.Contracts.Security;
using eTicketing.Ticketing.Business.Security;
using eTicketing.Ticketing.Data.Entities;
using eTicketing.Ticketing.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace eTicketing.Ticketing.Business.GateDevices;

/// <summary>
/// Registration and lifecycle of the physical scanners standing at a product's gates.
///
/// The rule this class exists to enforce is <see cref="ValidateScopeAsync"/>: every sector a device
/// is scoped to must actually belong to the product the device is registered for. Without it you
/// could register "Ulaz A" against tonight's concert but scope it to a sector of next week's match,
/// and the gate would then admit tickets it has no business admitting — the exact failure the
/// sector check was added to prevent, reintroduced one layer up.
/// </summary>
public class GateDeviceService : IGateDeviceService
{
    private readonly IGateDeviceRepository _repository;
    private readonly ICatalogClient _catalogClient;
    private readonly IProductSnapshotRepository _productSnapshots;
    private readonly GateDeviceKeyGenerator _keyGenerator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GateDeviceService> _logger;

    public GateDeviceService(
        IGateDeviceRepository repository,
        ICatalogClient catalogClient,
        IProductSnapshotRepository productSnapshots,
        GateDeviceKeyGenerator keyGenerator,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<GateDeviceService> logger)
    {
        _repository = repository;
        _catalogClient = catalogClient;
        _productSnapshots = productSnapshots;
        _keyGenerator = keyGenerator;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<PagedResult<GateDeviceResponse>>> SearchAsync(
        GateDeviceQuery query, ClaimsPrincipal user, CancellationToken ct = default)
    {
        // null organizationId => PlatformStaff, no tenancy filter — same convention as
        // TicketValidationService.GetProductsForTodayAsync.
        var isPlatformStaff = user.IsPlatformStaff();
        var organizationId = isPlatformStaff ? null : user.GetOrganizationId();
        if (organizationId is null && !isPlatformStaff)
            return Result<PagedResult<GateDeviceResponse>>.Failure(
                Error.Unauthorized("gate_device.no_organization", "Nalog nije vezan za organizaciju."));

        var paged = await _repository.SearchAsync(query, organizationId, query.ProductId, ct);

        return Result<PagedResult<GateDeviceResponse>>.Success(new PagedResult<GateDeviceResponse>
        {
            Items = paged.Items.Select(ToResponse).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize,
        });
    }

    public async Task<Result<GateDeviceResponse>> GetByIdAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var device = await _repository.GetByIdWithSectorsAsync(id, ct);
        if (device is null)
            return Result<GateDeviceResponse>.Failure(NotFound());

        var ownershipError = AuthorizeOwnership(user, device.OrganizationId);
        if (ownershipError is not null)
            return Result<GateDeviceResponse>.Failure(ownershipError);

        return Result<GateDeviceResponse>.Success(ToResponse(device));
    }

    public async Task<Result<GateDeviceCreatedResponse>> CreateAsync(
        UpsertGateDeviceRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var scope = await ValidateScopeAsync(request, user, ct);
        if (scope.IsFailure)
            return Result<GateDeviceCreatedResponse>.Failure(scope.Error);

        var product = scope.Value!;
        var key = _keyGenerator.Create();

        var device = new GateDevice
        {
            Id = Guid.NewGuid(),
            OrganizationId = product.OrganizationId,
            ProductId = request.ProductId,
            Name = request.Name.Trim(),
            KeyHash = key.KeyHash,
            KeyPrefix = key.KeyPrefix,
            AllSectors = request.AllSectors,
            IsActive = request.IsActive,
            CreatedByUserId = user.GetUserId(),
        };

        foreach (var sectorId in DistinctSectorIds(request))
        {
            device.Sectors.Add(new GateDeviceSector { Id = Guid.NewGuid(), SectorId = sectorId });
        }

        await _repository.AddAsync(device, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Registrovan ulazni uređaj {DeviceId} ({Name}) za proizvod {ProductId}, sektora: {SectorCount}, svi sektori: {AllSectors}.",
            device.Id, device.Name, device.ProductId, device.Sectors.Count, device.AllSectors);

        return Result<GateDeviceCreatedResponse>.Success(
            new GateDeviceCreatedResponse(await ReloadResponseAsync(device, ct), key.ApiKey));
    }

    public async Task<Result<GateDeviceResponse>> UpdateAsync(
        Guid id, UpsertGateDeviceRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var device = await _repository.GetByIdWithSectorsAsync(id, ct);
        if (device is null)
            return Result<GateDeviceResponse>.Failure(NotFound());

        var ownershipError = AuthorizeOwnership(user, device.OrganizationId);
        if (ownershipError is not null)
            return Result<GateDeviceResponse>.Failure(ownershipError);

        // Immutable for the same reason Sector.ProductId is: re-parenting would silently change
        // OrganizationId and orphan the sector scope, and a gate that quietly starts admitting a
        // different event is exactly the outcome this feature is meant to make impossible.
        if (request.ProductId != device.ProductId)
            return Result<GateDeviceResponse>.Failure(Error.Validation(
                "gate_device.product_id_immutable", "Uređaj se ne može premjestiti na drugi proizvod."));

        var scope = await ValidateScopeAsync(request, user, ct);
        if (scope.IsFailure)
            return Result<GateDeviceResponse>.Failure(scope.Error);

        device.Name = request.Name.Trim();
        device.AllSectors = request.AllSectors;
        device.IsActive = request.IsActive;

        ApplySectorSet(device, DistinctSectorIds(request));

        await _unitOfWork.SaveChangesAsync(ct);

        return Result<GateDeviceResponse>.Success(await ReloadResponseAsync(device, ct));
    }

    public async Task<Result<GateDeviceCreatedResponse>> RotateKeyAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var device = await _repository.GetByIdWithSectorsAsync(id, ct);
        if (device is null)
            return Result<GateDeviceCreatedResponse>.Failure(NotFound());

        var ownershipError = AuthorizeOwnership(user, device.OrganizationId);
        if (ownershipError is not null)
            return Result<GateDeviceCreatedResponse>.Failure(ownershipError);

        var key = _keyGenerator.Create();
        device.KeyHash = key.KeyHash;
        device.KeyPrefix = key.KeyPrefix;

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Rotiran ključ za ulazni uređaj {DeviceId} ({Name}).", device.Id, device.Name);

        return Result<GateDeviceCreatedResponse>.Success(
            new GateDeviceCreatedResponse(ToResponse(device), key.ApiKey));
    }

    public async Task<Result> DeleteAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var device = await _repository.GetByIdWithSectorsAsync(id, ct);
        if (device is null)
            return Result.Failure(NotFound());

        var ownershipError = AuthorizeOwnership(user, device.OrganizationId);
        if (ownershipError is not null)
            return Result.Failure(ownershipError);

        // Hard delete, per the platform's no-soft-delete rule. The join rows go with it by cascade.
        _repository.Remove(device);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<GateConfigResponse>> GetConfigAsync(GateDevice device, CancellationToken ct = default)
    {
        // The local read model, not Catalog: this is the device's own path (every boot and every
        // refresh), and like validation it should keep working while Catalog is down. Registration
        // below still asks Catalog, because that is an organizer's write and the ownership check
        // there wants the authoritative answer.
        var product = await _productSnapshots.GetByIdNoTrackingAsync(device.ProductId, ct);
        if (product is null)
            return Result<GateConfigResponse>.Failure(Error.NotFound(
                "gate_device.product_not_found", "Proizvod za ovaj uređaj više ne postoji."));

        // A device scoped to specific sectors needs their names for its own logs; an all-sectors
        // device has nothing to list, and asking the DB for every sector of the product just to
        // throw the names away would be wasted work at every gate boot.
        var sectors = device.AllSectors
            ? []
            : await ResolveSectorNamesAsync(device, ct);

        return Result<GateConfigResponse>.Success(new GateConfigResponse(
            DeviceId: device.Id,
            DeviceName: device.Name,
            ProductId: device.ProductId,
            ProductName: product.Name,
            ProductDate: product.Date,
            AllSectors: device.AllSectors,
            Sectors: sectors,
            ServerTime: _timeProvider.GetUtcNow().UtcDateTime));
    }

    public async Task TouchAsync(Guid deviceId, CancellationToken ct = default)
    {
        try
        {
            var device = await _repository.GetByIdAsync(deviceId, ct);
            if (device is null)
                return;

            device.LastSeenAt = _timeProvider.GetUtcNow().UtcDateTime;
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Swallowed on purpose, and the only place in this service that swallows anything.
            // LastSeenAt is a convenience column in a back-office list; letting a write failure on
            // it bubble up would turn a healthy ticket into a 500 at the door.
            _logger.LogWarning(ex, "Nije moguće ažurirati LastSeenAt za uređaj {DeviceId}.", deviceId);
        }
    }

    /// <summary>
    /// The cross-entity rules, shared by create and update: the product must exist and belong to the
    /// caller's organization (PlatformStaff bypasses), and every requested sector must belong to
    /// that same product. Field-level rules ran at the endpoint filter via
    /// UpsertGateDeviceRequestValidator.
    /// </summary>
    private async Task<Result<CatalogProductResponse>> ValidateScopeAsync(
        UpsertGateDeviceRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var product = await _catalogClient.GetProductAsync(request.ProductId, ct);
        if (product is null)
            return Result<CatalogProductResponse>.Failure(Error.Validation(
                "gate_device.product_not_found", "Odabrani proizvod ne postoji."));

        if (!user.IsPlatformStaff() && user.GetOrganizationId() != product.OrganizationId)
            return Result<CatalogProductResponse>.Failure(Error.Unauthorized(
                "gate_device.forbidden", "Proizvod ne pripada vašoj organizaciji."));

        if (request.AllSectors)
            return Result<CatalogProductResponse>.Success(product);

        var requested = DistinctSectorIds(request);
        if (requested.Count == 0)
            return Result<CatalogProductResponse>.Failure(Error.Validation(
                "gate_device.no_sectors", "Odaberite najmanje jedan sektor ili uključite opciju 'Svi sektori'."));

        var owned = await _repository.GetSectorIdsForProductAsync(request.ProductId, requested, ct);
        if (owned.Count != requested.Count)
            return Result<CatalogProductResponse>.Failure(Error.Validation(
                "gate_device.sector_not_in_product", "Odabrani sektor ne pripada ovom proizvodu."));

        return Result<CatalogProductResponse>.Success(product);
    }

    /// <summary>
    /// Diffs the requested sector set against what is already stored rather than clearing and
    /// re-adding: an unchanged sector keeps its row, its id and its CreatedAt, and the unique
    /// (GateDeviceId, SectorId) index is never asked to accept a delete and an insert of the same
    /// pair in one SaveChanges.
    ///
    /// Every add and remove goes through the repository rather than through the collection alone —
    /// see IGateDeviceRepository.AddSector for why leaving it to navigation fixup silently turns an
    /// insert into an UPDATE that matches no rows. The collection is kept in step too, because
    /// ToResponse reads it back straight after.
    /// </summary>
    private void ApplySectorSet(GateDevice device, List<Guid> requested)
    {
        var wanted = device.AllSectors ? [] : requested.ToHashSet();

        foreach (var existing in device.Sectors.Where(s => !wanted.Contains(s.SectorId)).ToList())
        {
            device.Sectors.Remove(existing);
            _repository.RemoveSector(existing);
        }

        var present = device.Sectors.Select(s => s.SectorId).ToHashSet();
        foreach (var sectorId in wanted.Where(id => !present.Contains(id)))
        {
            // Note the asymmetry with the removal above, which does touch the collection: EF's
            // navigation fixup appends a newly tracked dependent to the parent's loaded collection
            // by itself, so adding it here as well would put the same row in twice. Removal is
            // idempotent; addition is not.
            _repository.AddSector(new GateDeviceSector
            {
                Id = Guid.NewGuid(),
                GateDeviceId = device.Id,
                SectorId = sectorId,
            });
        }
    }

    /// <summary>Re-reads the device after a write so the response carries committed state with real
    /// sector names. Rows this request just inserted have no Sector navigation loaded, and rendering
    /// them straight from the in-memory graph would hand the back-office a list of nameless
    /// sectors.</summary>
    private async Task<GateDeviceResponse> ReloadResponseAsync(GateDevice device, CancellationToken ct) =>
        ToResponse(await _repository.GetByIdWithSectorsAsync(device.Id, ct) ?? device);

    private async Task<List<GateDeviceSectorResponse>> ResolveSectorNamesAsync(GateDevice device, CancellationToken ct)
    {
        var ids = device.Sectors.Select(s => s.SectorId).ToList();
        if (ids.Count == 0)
            return [];

        var names = await _repository.GetSectorNamesAsync(ids, ct);
        return names.Select(n => new GateDeviceSectorResponse(n.Id, n.Name)).ToList();
    }

    private static List<Guid> DistinctSectorIds(UpsertGateDeviceRequest request) =>
        request.AllSectors ? [] : request.SectorIds.Distinct().ToList();

    private static Error NotFound() => Error.NotFound("gate_device.not_found", "Ulazni uređaj nije pronađen.");

    private static Error? AuthorizeOwnership(ClaimsPrincipal user, Guid organizationId)
    {
        if (user.IsPlatformStaff())
            return null;

        return user.GetOrganizationId() != organizationId
            ? Error.Unauthorized("gate_device.forbidden", "Uređaj ne pripada vašoj organizaciji.")
            : null;
    }

    private static GateDeviceResponse ToResponse(GateDevice device) => new(
        Id: device.Id,
        ProductId: device.ProductId,
        Name: device.Name,
        KeyPrefix: device.KeyPrefix,
        AllSectors: device.AllSectors,
        IsActive: device.IsActive,
        LastSeenAt: device.LastSeenAt,
        CreatedAt: device.CreatedAt,
        // Sector may be null when the graph was loaded without the ThenInclude (the create path
        // builds join rows in memory and never round-trips them) — the id is always there.
        Sectors: device.Sectors
            .Select(s => new GateDeviceSectorResponse(s.SectorId, s.Sector?.Name ?? string.Empty))
            .OrderBy(s => s.Name)
            .ToList());
}
