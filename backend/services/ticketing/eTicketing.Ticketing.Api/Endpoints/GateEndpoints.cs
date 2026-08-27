using eTicketing.Contracts.Results;
using eTicketing.Contracts.Validation;
using eTicketing.Ticketing.Api.Infrastructure.Auth;
using eTicketing.Ticketing.Business.GateDevices;
using eTicketing.Ticketing.Business.Tickets;

namespace eTicketing.Ticketing.Api.Endpoints;

/// <summary>
/// The two calls an unattended scanner makes. Authenticated by <c>X-Device-Key</c>
/// (see GateDeviceAuthenticationHandler), never by a cookie or a bearer JWT.
///
/// Neither endpoint accepts a product or sector from the caller. That is the whole security
/// property: the device says "here is what I scanned", the server answers using the scope stored
/// against that device, so re-flashing a gate with tampered firmware cannot make it admit tickets
/// for another event or another sector.
/// </summary>
public static class GateEndpoints
{
    public static void MapGateEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/gate").WithTags("Gate")
            .RequireAuthorization(GateDeviceAuthenticationHandler.PolicyName);

        group.MapGet("/config", GetConfig);
        group.MapPost("/validate", Validate).WithValidation<GateValidateRequest>();
    }

    /// <summary>Fetched at boot and re-fetched on a timer, which is how a scope change made in the
    /// back-office reaches a gate without anyone re-flashing it.</summary>
    private static async Task<IResult> GetConfig(IGateDeviceService service, HttpContext http, CancellationToken ct)
    {
        var device = GateDeviceAuthenticationHandler.GetDevice(http)!;

        var result = await service.GetConfigAsync(device, ct);
        await service.TouchAsync(device.Id, ct);

        return result.ToHttpResult();
    }

    /// <summary>Same contract as the organizer-facing POST /tickets/validate: an invalid ticket is
    /// still 200 with IsValid=false and a Bosnian reason, because the gate has to show the holder
    /// why they were turned away. 409 means another scanner is mid-validation on this same ticket
    /// and the firmware should invite a retry rather than reject.</summary>
    private static async Task<IResult> Validate(
        GateValidateRequest request,
        ITicketValidationService validationService,
        IGateDeviceService deviceService,
        HttpContext http,
        CancellationToken ct)
    {
        var device = GateDeviceAuthenticationHandler.GetDevice(http)!;

        var result = await validationService.ValidateForDeviceAsync(device, request.Code, ct);

        // After the verdict, and never gating it: LastSeenAt is a back-office convenience column,
        // and a failure writing it must not be able to turn someone away at a door.
        await deviceService.TouchAsync(device.Id, ct);

        return result.ToHttpResult();
    }
}
