using System.Security.Claims;
using System.Text.Encodings.Web;
using eTicketing.Contracts.Security;
using eTicketing.Ticketing.Business.Security;
using eTicketing.Ticketing.Data.Entities;
using eTicketing.Ticketing.Data.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace eTicketing.Ticketing.Api.Infrastructure.Auth;

/// <summary>
/// Authenticates an unattended gate scanner by the <c>X-Device-Key</c> header.
///
/// This is a second, additive scheme — the shared JWT/cookie scheme stays the default, so nothing
/// else in Ticketing changes. It exists because the platform's normal credential is a 15-minute
/// httpOnly cookie obtained through an interactive login, which a microcontroller bolted to a
/// turnstile cannot hold, refresh, or be trusted with.
///
/// The authenticated device entity is parked on <see cref="HttpContext.Items"/> so the endpoint
/// that follows does not have to look it up a second time on the hot path of a queue at a door.
/// </summary>
public sealed class GateDeviceAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "GateDevice";
    public const string PolicyName = "GateDevice";
    public const string HeaderName = "X-Device-Key";
    public const string DeviceIdClaimType = "gate_device_id";

    private const string DeviceItemKey = "eTicketing.GateDevice";

    private readonly IGateDeviceRepository _repository;

    public GateDeviceAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IGateDeviceRepository repository)
        : base(options, logger, encoder)
    {
        _repository = repository;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values))
            return AuthenticateResult.NoResult();

        var apiKey = values.ToString();
        if (string.IsNullOrWhiteSpace(apiKey))
            return AuthenticateResult.NoResult();

        var device = await _repository.GetByKeyHashAsync(GateDeviceKeyGenerator.Hash(apiKey.Trim()));

        // One message for "no such key" and "key belongs to a disabled device", logged separately.
        // A device that has been deactivated because it went missing should not be able to confirm
        // to whoever is holding it that the key itself was ever real.
        if (device is null)
        {
            Logger.LogWarning("Odbijen pristup ulaznog uređaja: nepoznat ključ (prefiks {Prefix}).",
                apiKey.Length >= 16 ? apiKey[..16] : "?");
            return AuthenticateResult.Fail("Neispravan ključ uređaja.");
        }

        if (!device.IsActive)
        {
            Logger.LogWarning("Odbijen pristup deaktiviranog ulaznog uređaja {DeviceId} ({Name}).", device.Id, device.Name);
            return AuthenticateResult.Fail("Neispravan ključ uređaja.");
        }

        Context.Items[DeviceItemKey] = device;

        var claims = new List<Claim>
        {
            new(DeviceIdClaimType, device.Id.ToString()),
            // NameIdentifier is the organizer accountable for this gate, matching what gets written
            // to Ticket.ValidatedByUserId — so anything reading the caller's identity downstream
            // sees a real person, not a device masquerading as one.
            new(ClaimTypes.NameIdentifier, device.CreatedByUserId.ToString()),
            new("organizationId", device.OrganizationId.ToString()),
            new(ClaimTypes.Name, device.Name),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    /// <summary>The device this request authenticated as. Non-null inside any endpoint guarded by
    /// the <see cref="PolicyName"/> policy — the handler cannot succeed without setting it.</summary>
    public static GateDevice? GetDevice(HttpContext context) =>
        context.Items.TryGetValue(DeviceItemKey, out var device) ? device as GateDevice : null;
}
