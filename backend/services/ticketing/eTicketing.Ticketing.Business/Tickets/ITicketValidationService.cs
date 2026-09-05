using System.Security.Claims;
using eTicketing.Contracts.Results;
using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Business.Tickets;

public interface ITicketValidationService
{
    /// <summary>Organizer — products of the caller's own organization with tickets admitting entry
    /// TODAY. PlatformStaff sees every organization's.</summary>
    Task<Result<List<ValidationProductResponse>>> GetProductsForTodayAsync(ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Scans one code against one product — optionally narrowed to specific sectors via
    /// <see cref="ValidateTicketRequest.SectorIds"/> — and, if everything checks out, burns the
    /// ticket (Status → Used) so it can never be admitted twice.</summary>
    Task<Result<TicketValidationResponse>> ValidateAsync(ValidateTicketRequest request, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>The unattended-scanner path. Product and sector scope come from the device's own
    /// registration rather than from the request, so a device cannot widen what its door admits;
    /// the burn is attributed to the organizer who registered it, plus the device id.</summary>
    Task<Result<TicketValidationResponse>> ValidateForDeviceAsync(GateDevice device, string code, CancellationToken ct = default);
}
