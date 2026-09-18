using System.Security.Claims;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Contracts.Security;
using eTicketing.Ticketing.Business.Security;
using eTicketing.Ticketing.Data.Entities;
using eTicketing.Ticketing.Data.Repositories;
using Mapster;

namespace eTicketing.Ticketing.Business.Sectors;

public class TicketTypeService : ITicketTypeService
{
    private readonly ISectorRepository _sectorRepository;
    private readonly ITicketTypeRepository _ticketTypeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TicketTypeService(ISectorRepository sectorRepository, ITicketTypeRepository ticketTypeRepository, IUnitOfWork unitOfWork)
    {
        _sectorRepository = sectorRepository;
        _ticketTypeRepository = ticketTypeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<List<TicketTypeResponse>>> GetBySectorAsync(Guid sectorId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var sector = await _sectorRepository.GetByIdAsync(sectorId, ct);
        if (sector is null)
            return Result<List<TicketTypeResponse>>.Failure(Error.NotFound("sector.not_found", "Sektor nije pronađen."));

        // Same NotFound as an unknown sector, deliberately: a 403 here would tell an anonymous
        // caller that the id names a real, unpublished sector. The owning organizer still needs
        // this read on a Draft — that is the whole ticket-type step of the create-then-publish
        // flow in the desktop sector dialog — so ownership, not publication alone, is the gate.
        if (sector.Status != PublishStatus.Published && AuthorizeOwnership(user, sector.OrganizationId) is not null)
            return Result<List<TicketTypeResponse>>.Failure(Error.NotFound("sector.not_found", "Sektor nije pronađen."));

        var ticketTypes = await _ticketTypeRepository.GetBySectorIdAsync(sectorId, ct);
        return Result<List<TicketTypeResponse>>.Success(ticketTypes.Select(ToResponse).ToList());
    }

    public async Task<Result<TicketTypeResponse>> CreateAsync(Guid sectorId, UpsertTicketTypeRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var sector = await _sectorRepository.GetByIdAsync(sectorId, ct);
        if (sector is null)
            return Result<TicketTypeResponse>.Failure(Error.NotFound("sector.not_found", "Sektor nije pronađen."));

        var ownershipError = AuthorizeOwnership(user, sector.OrganizationId);
        if (ownershipError is not null)
            return Result<TicketTypeResponse>.Failure(ownershipError);

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            SectorId = sectorId,
            Name = request.Name,
            Price = request.Price,
        };

        await _ticketTypeRepository.AddAsync(ticketType, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<TicketTypeResponse>.Success(ToResponse(ticketType));
    }

    public async Task<Result<TicketTypeResponse>> UpdateAsync(Guid sectorId, Guid id, UpsertTicketTypeRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var sector = await _sectorRepository.GetByIdAsync(sectorId, ct);
        if (sector is null)
            return Result<TicketTypeResponse>.Failure(Error.NotFound("sector.not_found", "Sektor nije pronađen."));

        var ownershipError = AuthorizeOwnership(user, sector.OrganizationId);
        if (ownershipError is not null)
            return Result<TicketTypeResponse>.Failure(ownershipError);

        var ticketType = await _ticketTypeRepository.GetByIdAsync(id, ct);
        if (ticketType is null || ticketType.SectorId != sectorId)
            return Result<TicketTypeResponse>.Failure(Error.NotFound("ticket_type.not_found", "Tip ulaznice nije pronađen."));

        ticketType.Name = request.Name;
        ticketType.Price = request.Price;

        await _unitOfWork.SaveChangesAsync(ct);

        return Result<TicketTypeResponse>.Success(ToResponse(ticketType));
    }

    public async Task<Result> DeleteAsync(Guid sectorId, Guid id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var sector = await _sectorRepository.GetByIdAsync(sectorId, ct);
        if (sector is null)
            return Result.Failure(Error.NotFound("sector.not_found", "Sektor nije pronađen."));

        var ownershipError = AuthorizeOwnership(user, sector.OrganizationId);
        if (ownershipError is not null)
            return Result.Failure(ownershipError);

        var ticketType = await _ticketTypeRepository.GetByIdAsync(id, ct);
        if (ticketType is null || ticketType.SectorId != sectorId)
            return Result.Failure(Error.NotFound("ticket_type.not_found", "Tip ulaznice nije pronađen."));

        _ticketTypeRepository.Remove(ticketType);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    /// <summary>PlatformStaff bypasses ownership entirely; everyone else must own the sector's
    /// organization. Duplicated from SectorService — same small-per-service pattern used
    /// throughout this codebase, no shared base class for it.</summary>
    private static Error? AuthorizeOwnership(ClaimsPrincipal user, Guid organizationId)
    {
        if (user.IsPlatformStaff())
            return null;

        if (user.GetOrganizationId() != organizationId)
            return Error.Unauthorized("ticket_type.forbidden", "Sektor ne pripada vašoj organizaciji.");

        return null;
    }

    private static TicketTypeResponse ToResponse(TicketType ticketType) => ticketType.Adapt<TicketTypeResponse>();
}
