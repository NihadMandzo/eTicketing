using System.Security.Claims;
using System.Text.Encodings.Web;
using eTicketing.Contracts.Security;
using eTicketing.Ticketing.Business.Security;
using eTicketing.Ticketing.Data.Entities;
using eTicketing.Ticketing.Data.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace eTicketing.Ticketing.Api.Infrastructure.Auth;

public static class GateDeviceAuthenticationExtensions
{
    /// <summary>Registers the device scheme <b>alongside</b> the shared JWT/cookie one. Call after
    /// AddSharedJwtBearerAuthentication — the parameterless AddAuthentication() here deliberately
    /// does not restate a default scheme, so JWT remains the default for every other endpoint.</summary>
    public static IServiceCollection AddGateDeviceAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, GateDeviceAuthenticationHandler>(
                GateDeviceAuthenticationHandler.SchemeName, _ => { });

        // Pinned to its own scheme in both directions: an organizer's cookie can never satisfy this
        // policy, and a device key can never satisfy "Organizer". A device is not a small organizer
        // — it can do exactly two things, and the type system should say so.
        services.AddAuthorizationBuilder()
            .AddPolicy(GateDeviceAuthenticationHandler.PolicyName, policy => policy
                .AddAuthenticationSchemes(GateDeviceAuthenticationHandler.SchemeName)
                .RequireClaim(GateDeviceAuthenticationHandler.DeviceIdClaimType));

        return services;
    }
}
