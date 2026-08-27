using System.Security.Claims;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Results;
using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Business.GateDevices;

public interface IGateDeviceService
{
    Task<Result<PagedResult<GateDeviceResponse>>> SearchAsync(GateDeviceQuery query, ClaimsPrincipal user, CancellationToken ct = default);

    Task<Result<GateDeviceResponse>> GetByIdAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Registers a gate and mints its key. The plaintext key is in the response and
    /// nowhere else, ever again.</summary>
    Task<Result<GateDeviceCreatedResponse>> CreateAsync(UpsertGateDeviceRequest request, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Rename, re-scope to a different sector set, enable/disable. ProductId is immutable —
    /// re-pointing a gate at another event is a new device, not an edit.</summary>
    Task<Result<GateDeviceResponse>> UpdateAsync(Guid id, UpsertGateDeviceRequest request, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Mints a replacement key and invalidates the old one immediately — the response to a
    /// device being lost or a key being pasted somewhere public.</summary>
    Task<Result<GateDeviceCreatedResponse>> RotateKeyAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Device-facing: what this gate is guarding right now. Called by the firmware at boot
    /// and on a refresh timer, which is how a re-scope takes effect without a re-flash.</summary>
    Task<Result<GateConfigResponse>> GetConfigAsync(GateDevice device, CancellationToken ct = default);

    /// <summary>Stamps LastSeenAt so the back-office can tell a live gate from a dead one. Called on
    /// every authenticated device request; failures here are logged, never surfaced — a bookkeeping
    /// column must not be able to turn someone away at a door.</summary>
    Task TouchAsync(Guid deviceId, CancellationToken ct = default);
}
