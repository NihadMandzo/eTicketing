using eTicketing.Contracts.Results;
using eTicketing.Contracts.Validation;
using eTicketing.Ticketing.Business.GateDevices;

namespace eTicketing.Ticketing.Api.Endpoints;

/// <summary>Back-office management of gate scanners. "Organizer" also admits Admin/SuperAdmin (see
/// AuthorizationPolicyExtensions) — the service then skips the per-organization ownership check for
/// them, same as every other organizer-facing surface in this service.</summary>
public static class GateDeviceEndpoints
{
    public static void MapGateDeviceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/gate-devices").WithTags("GateDevices").RequireAuthorization("Organizer");

        group.MapGet("", Search).WithValidation<GateDeviceQuery>();
        group.MapGet("/{id:guid}", GetById);
        group.MapPost("", Create).WithValidation<UpsertGateDeviceRequest>();
        group.MapPut("/{id:guid}", Update).WithValidation<UpsertGateDeviceRequest>();
        group.MapPost("/{id:guid}/rotate-key", RotateKey);
        group.MapDelete("/{id:guid}", Delete);
    }

    private static async Task<IResult> Search(
        [AsParameters] GateDeviceQuery query, IGateDeviceService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.SearchAsync(query, http.User, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetById(Guid id, IGateDeviceService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.GetByIdAsync(id, http.User, ct);
        return result.ToHttpResult();
    }

    /// <summary>The response carries the plaintext API key, and this is the only place besides
    /// rotate-key that ever will — the server keeps only its hash. The client is responsible for
    /// showing it once and not persisting it.</summary>
    private static async Task<IResult> Create(
        UpsertGateDeviceRequest request, IGateDeviceService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, http.User, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Update(
        Guid id, UpsertGateDeviceRequest request, IGateDeviceService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.UpdateAsync(id, request, http.User, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> RotateKey(Guid id, IGateDeviceService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.RotateKeyAsync(id, http.User, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Delete(Guid id, IGateDeviceService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.DeleteAsync(id, http.User, ct);
        return result.IsSuccess ? Results.NoContent() : result.ToHttpResult();
    }
}
