using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace eTicketing.Contracts.Hosting;

/// <summary>
/// Caps how many requests one service instance works on at once, and refuses the overflow with
/// <c>503 Service Unavailable</c> + <c>Retry-After</c> straight away.
///
/// <para><b>Why.</b> Without it, an overloaded service accepts everything. Requests then queue on
/// the SQL connection pool, each waits out its timeout, and the whole service stops answering
/// anyone — measured at 10,000 load-test users as 71% of requests failing after 1-3 minutes
/// (docs/load-testing-report.md). With it, the requests that are admitted stay fast and the rest
/// get an answer in milliseconds that the clients can retry.</para>
///
/// <para>A separate middleware rather than a global limiter on the ASP.NET Core rate-limiting
/// middleware: Identity already uses that for its per-IP login limit, with its own 429 rejection
/// body, and a service has only one <c>OnRejected</c> — overload and "too many login attempts"
/// would have been reported as the same thing.</para>
///
/// <para><c>/health</c> is never shed, so an orchestrator does not restart a container for being
/// busy.</para>
/// </summary>
public sealed class LoadSheddingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ConcurrencyLimiter _limiter;
    private readonly int _retryAfterSeconds;

    public LoadSheddingMiddleware(RequestDelegate next, IOptions<LoadSheddingOptions> options)
    {
        _next = next;
        var value = options.Value;
        _retryAfterSeconds = value.RetryAfterSeconds;
        _limiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = value.MaxConcurrentRequests,
            QueueLimit = value.QueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        using var lease = await _limiter.AcquireAsync(1, context.RequestAborted);
        if (!lease.IsAcquired)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.RetryAfter = _retryAfterSeconds.ToString();
            await context.Response.WriteAsJsonAsync(
                new
                {
                    Code = "server.overloaded",
                    Message = "Sistem je trenutno preopterećen. Pokušajte ponovo za nekoliko sekundi."
                },
                context.RequestAborted);
            return;
        }

        await _next(context);
    }
}
