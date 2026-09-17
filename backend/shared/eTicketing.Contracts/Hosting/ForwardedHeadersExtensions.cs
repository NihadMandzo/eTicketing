using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace eTicketing.Contracts.Hosting;

/// <summary>
/// Makes <c>HttpContext.Connection.RemoteIpAddress</c> hold the real caller rather than whichever
/// proxy happened to forward the request. Nothing in this platform is reachable except through the
/// Gateway, so without this every request inside a service looks like it came from the Gateway's
/// container IP — which silently turned the per-IP login/email rate limiter into one global bucket
/// for the entire platform.
/// </summary>
public static class ForwardedHeadersExtensions
{
    /// <summary>
    /// Binds <see cref="ForwardedHeadersOptions"/> from the <c>ForwardedHeaders</c> configuration
    /// section and clears the known-proxy allowlist.
    ///
    /// <para><b>Why clearing KnownNetworks/KnownProxies is safe here, and why it would not be
    /// elsewhere.</b> The default allowlist is loopback only, which means the header is ignored
    /// outright in containers — the middleware appears to be working and changes nothing. The
    /// alternative, enumerating the container network, is unstable: Docker and Azure Container Apps
    /// both assign those ranges dynamically. Clearing the list trusts whoever sends the header, and
    /// that is only acceptable because these services publish no port of their own
    /// (docker-compose.yml gives one to the Gateway alone) — there is no path to them that does not
    /// already pass through it.</para>
    ///
    /// <para><b><see cref="ForwardedHeadersOptions.ForwardLimit"/> must match the real hop count,
    /// and is configuration, not a constant, because it differs per deployment.</b> The middleware
    /// walks <c>X-Forwarded-For</c> right-to-left, one entry per hop, and stops after this many —
    /// so the number *is* the trust boundary. Local compose is one hop (Gateway → service).
    /// Production through the web tier's proxy is three (ingress → Node → Gateway → service), while
    /// the Flutter clients reach the same service in two, because they skip the web tier entirely.
    /// Setting it higher than a given path's real depth lets a caller on that path prepend a
    /// fabricated entry and be believed — so size it to the *shortest* chain, and treat the limiter
    /// as defence in depth rather than an authorization boundary. Default 1: correct for compose,
    /// and the value that fails closed (one global bucket again) rather than spoofably open if a
    /// deployment forgets to set it. Production sets <c>ForwardedHeaders__ForwardLimit=2</c> in
    /// <c>.github/workflows/identity-service.yml</c>, the only service that calls this.</para>
    /// </summary>
    public static IServiceCollection AddPlatformForwardedHeaders(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = configuration.GetValue<int?>("ForwardedHeaders:ForwardLimit") ?? 1;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return services;
    }
}
