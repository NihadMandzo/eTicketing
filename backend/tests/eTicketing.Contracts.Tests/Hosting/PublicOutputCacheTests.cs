using System.Net;
using eTicketing.Contracts.Hosting;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace eTicketing.Contracts.Tests.Hosting;

/// <summary>
/// PublicOutputCachePolicy and OutputCacheEvictionFilter, through the real output-cache middleware.
/// Every endpoint below returns how many times its handler has run, so "served from cache" is
/// simply "the number did not go up".
/// </summary>
public class PublicOutputCacheTests : IAsyncDisposable
{
    private const string Tag = "test-public";
    private int _hits;
    private WebApplication? _app;

    [Fact]
    public async Task Get_SecondRequestWithinTheDuration_IsServedFromCache()
    {
        var client = await StartAsync();

        var first = await client.GetStringAsync("/items?page=0");
        var second = await client.GetStringAsync("/items?page=0");

        first.Should().Be("1");
        second.Should().Be("1");
    }

    [Fact]
    public async Task Get_DifferentQueryString_IsASeparateEntry()
    {
        var client = await StartAsync();

        await client.GetStringAsync("/items?page=0");
        var otherPage = await client.GetStringAsync("/items?page=1");

        otherPage.Should().Be("2");
    }

    [Fact]
    public async Task Get_CallerWithCredentials_IsStillServedFromCache()
    {
        // The framework's default policy would refuse this request; the public policy must not,
        // or every signed-in buyer would miss the cache.
        var client = await StartAsync();
        await client.GetStringAsync("/items");

        var request = new HttpRequestMessage(HttpMethod.Get, "/items");
        request.Headers.Add("Authorization", "Bearer anything");
        request.Headers.Add("Cookie", "eticketing_at=anything");
        var response = await client.SendAsync(request);

        (await response.Content.ReadAsStringAsync()).Should().Be("1");
    }

    [Fact]
    public async Task Get_NotFoundResponse_IsNeverStored()
    {
        var client = await StartAsync();

        await client.GetAsync("/missing");
        await client.GetAsync("/missing");

        _hits.Should().Be(2);
    }

    [Fact]
    public async Task Get_ResponseThatSetsACookie_IsNeverStored()
    {
        var client = await StartAsync();

        await client.GetAsync("/with-cookie");
        await client.GetAsync("/with-cookie");

        _hits.Should().Be(2);
    }

    [Fact]
    public async Task Post_IsNeverCached()
    {
        var client = await StartAsync();

        await client.PostAsync("/items", null);
        var second = await (await client.PostAsync("/items", null)).Content.ReadAsStringAsync();

        second.Should().Be("2");
    }

    [Fact]
    public async Task EvictsPublicCache_AfterAWrite_TheNextReadIsFresh()
    {
        var client = await StartAsync();
        (await client.GetStringAsync("/items")).Should().Be("1");

        await client.PostAsync("/write", null);

        (await client.GetStringAsync("/items")).Should().Be("2");
    }

    [Fact]
    public async Task Get_AfterTheDurationExpires_IsFetchedAgain()
    {
        var client = await StartAsync(TimeSpan.FromMilliseconds(200));
        await client.GetStringAsync("/items");

        await Task.Delay(400);

        (await client.GetStringAsync("/items")).Should().Be("2");
    }

    private async Task<HttpClient> StartAsync(TimeSpan? duration = null)
    {
        var lifetime = duration ?? TimeSpan.FromMinutes(1);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddOutputCache();

        _app = builder.Build();
        _app.UseOutputCache();
        _app.MapGet("/items", () => Interlocked.Increment(ref _hits).ToString()).CachePublicly(lifetime, Tag);
        _app.MapPost("/items", () => Interlocked.Increment(ref _hits).ToString()).CachePublicly(lifetime, Tag);
        _app.MapGet("/missing", () =>
        {
            Interlocked.Increment(ref _hits);
            return TypedResults.NotFound();
        }).CachePublicly(lifetime, Tag);
        _app.MapGet("/with-cookie", (HttpContext http) =>
        {
            Interlocked.Increment(ref _hits);
            http.Response.Cookies.Append("session", "x");
            return TypedResults.Ok();
        }).CachePublicly(lifetime, Tag);
        _app.MapPost("/write", () => TypedResults.NoContent()).EvictsPublicCache(Tag);

        await _app.StartAsync();
        return _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null) await _app.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
