using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;

namespace eTicketing.Contracts.Hosting;

/// <summary>
/// Output-cache policy for the anonymous browse endpoints, where every visitor gets the same
/// bytes: the published product list, a published product, categories, popular recommendations,
/// the public sector list.
///
/// <para><b>Why not the framework's default policy.</b> The default refuses any request that
/// carries credentials, and here a signed-in buyer's session cookie authenticates every request
/// it rides on — including these. Most browse traffic would then miss the cache for no reason,
/// because the responses do not depend on who is asking. This policy ignores the caller, which is
/// exactly why it may only be put on endpoints whose response is caller-independent. An endpoint
/// that reads <c>HttpContext.User</c> must never carry it.</para>
///
/// <para>Only 200 responses without <c>Set-Cookie</c> are stored, so a validation error, a 404 or
/// anything session-shaped never ends up served to the next visitor. Concurrent misses on the same
/// key are collapsed into one database round-trip (<see cref="OutputCacheContext.AllowLocking"/>) —
/// the stampede when a popular page expires is precisely the load this is for.</para>
/// </summary>
public sealed class PublicOutputCachePolicy : IOutputCachePolicy
{
    private readonly TimeSpan _duration;
    private readonly string _tag;

    public PublicOutputCachePolicy(TimeSpan duration, string tag)
    {
        _duration = duration;
        _tag = tag;
    }

    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        var method = context.HttpContext.Request.Method;
        var cacheable = HttpMethods.IsGet(method) || HttpMethods.IsHead(method);

        context.EnableOutputCaching = true;
        context.AllowCacheLookup = cacheable;
        context.AllowCacheStorage = cacheable;
        context.AllowLocking = true;
        context.ResponseExpirationTimeSpan = _duration;
        // Page, PageSize, CategoryId, City, ProductId, Take ... — every filter is part of the key.
        context.CacheVaryByRules.QueryKeys = "*";
        context.Tags.Add(_tag);

        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellation) =>
        ValueTask.CompletedTask;

    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        var response = context.HttpContext.Response;
        if (response.StatusCode != StatusCodes.Status200OK || response.Headers.SetCookie.Count > 0)
            context.AllowCacheStorage = false;

        return ValueTask.CompletedTask;
    }
}
