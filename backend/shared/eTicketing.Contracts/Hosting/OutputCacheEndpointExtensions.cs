using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace eTicketing.Contracts.Hosting;

/// <summary>
/// The two halves of the public-response cache, as route builder calls: <see cref="CachePublicly"/>
/// on the anonymous reads, <see cref="EvictsPublicCache"/> on every write that changes what those
/// reads return. Both take the same tag, which is what ties a write to the reads it invalidates.
/// </summary>
public static class OutputCacheEndpointExtensions
{
    /// <summary>Caches the response for every caller alike — see <see cref="PublicOutputCachePolicy"/>
    /// for why this is only for endpoints whose response does not depend on the caller.</summary>
    public static RouteHandlerBuilder CachePublicly(this RouteHandlerBuilder builder, TimeSpan duration, string tag) =>
        builder.CacheOutput(new PublicOutputCachePolicy(duration, tag));

    public static RouteHandlerBuilder EvictsPublicCache(this RouteHandlerBuilder builder, string tag) =>
        builder.AddEndpointFilter(new OutputCacheEvictionFilter(tag));
}
