using System.Net;
using System.Net.Http.Json;
using eTicketing.Contracts.Hosting;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace eTicketing.Contracts.Tests.Hosting;

public class LoadSheddingMiddlewareTests : IAsyncDisposable
{
    // Holds /slow open until the test lets it go, so "a request is in flight" is a fact, not a race.
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private WebApplication? _app;

    [Fact]
    public async Task InvokeAsync_UnderTheLimit_PassesTheRequestThrough()
    {
        var client = await StartAsync(maxConcurrent: 1, queueLimit: 0);

        var response = await client.GetAsync("/fast");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InvokeAsync_WhenEverySlotAndQueuePlaceIsTaken_Returns503WithRetryAfterAndCode()
    {
        var client = await StartAsync(maxConcurrent: 1, queueLimit: 0);
        var inFlight = client.GetAsync("/slow");
        await _entered.Task;

        var refused = await client.GetAsync("/fast");

        refused.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        refused.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(2));
        var body = await refused.Content.ReadFromJsonAsync<ErrorResponseBody>();
        body!.Code.Should().Be("server.overloaded");
        body.Message.Should().Be("Sistem je trenutno preopterećen. Pokušajte ponovo za nekoliko sekundi.");

        _release.SetResult();
        (await inFlight).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InvokeAsync_HealthEndpoint_IsNeverShed()
    {
        var client = await StartAsync(maxConcurrent: 1, queueLimit: 0);
        var inFlight = client.GetAsync("/slow");
        await _entered.Task;

        var health = await client.GetAsync("/health");

        health.StatusCode.Should().Be(HttpStatusCode.OK);
        _release.SetResult();
        await inFlight;
    }

    [Fact]
    public async Task InvokeAsync_QueuedRequest_RunsOnceASlotFrees()
    {
        var client = await StartAsync(maxConcurrent: 1, queueLimit: 1);
        var inFlight = client.GetAsync("/slow");
        await _entered.Task;

        var queued = client.GetAsync("/fast");
        await Task.Delay(100);
        queued.IsCompleted.Should().BeFalse("it waits in the queue rather than being refused");

        _release.SetResult();
        (await queued).StatusCode.Should().Be(HttpStatusCode.OK);
        (await inFlight).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InvokeAsync_AfterTheBurst_AcceptsRequestsAgain()
    {
        var client = await StartAsync(maxConcurrent: 1, queueLimit: 0);
        var inFlight = client.GetAsync("/slow");
        await _entered.Task;
        (await client.GetAsync("/fast")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        _release.SetResult();
        await inFlight;

        (await client.GetAsync("/fast")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<HttpClient> StartAsync(int maxConcurrent, int queueLimit)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.Configure<LoadSheddingOptions>(o =>
        {
            o.MaxConcurrentRequests = maxConcurrent;
            o.QueueLimit = queueLimit;
            o.RetryAfterSeconds = 2;
        });

        _app = builder.Build();
        _app.UsePlatformLoadShedding();
        _app.MapGet("/fast", () => TypedResults.Ok());
        _app.MapGet("/slow", async () =>
        {
            _entered.TrySetResult();
            await _release.Task;
            return TypedResults.Ok();
        });
        _app.MapGet("/health", () => TypedResults.Ok());

        await _app.StartAsync();
        return _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _release.TrySetResult();
        if (_app is not null) await _app.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
