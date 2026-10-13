using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.DependencyInjection;

namespace eTicketing.Contracts.Hosting;

/// <summary>
/// Evicts every cached response under one tag after a write endpoint runs, so an organizer who
/// publishes or edits something sees it on the public pages straight away instead of after the
/// cache expires.
///
/// <para>Evicts whatever the outcome. Telling a successful write from a failed one would mean
/// inspecting the result type, and an unneeded eviction costs one extra database read on the
/// next browse request — writes are rare next to reads, so the simple rule is the right one.</para>
///
/// <para>The cache is in-process, so with several replicas this only clears the one that served
/// the write; the others catch up when their entries expire. That is why the cached lifetimes
/// stay short (seconds, not minutes).</para>
/// </summary>
public sealed class OutputCacheEvictionFilter : IEndpointFilter
{
    private readonly string _tag;

    public OutputCacheEvictionFilter(string tag)
    {
        _tag = tag;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        finally
        {
            var store = context.HttpContext.RequestServices.GetRequiredService<IOutputCacheStore>();
            await store.EvictByTagAsync(_tag, CancellationToken.None);
        }
    }
}
