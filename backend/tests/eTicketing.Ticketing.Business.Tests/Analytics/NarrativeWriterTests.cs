using System.Net;
using System.Text;
using System.Text.Json;
using eTicketing.Ticketing.Api.Infrastructure;
using eTicketing.Ticketing.Business.Analytics;
using eTicketing.Ticketing.Business.Analytics.Narrative;
using eTicketing.Ticketing.Business.Reports;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace eTicketing.Ticketing.Business.Tests.Analytics;

/// <summary>
/// The narrative writer's contract is almost entirely about failure. Exactly one path returns text;
/// every other path — a 500, a timeout, HTML from a proxy, an empty completion, a shape the
/// provider changed — must return null without throwing, because the report is already computed by
/// the time this runs and losing it over an optional summary would be the worst possible trade.
/// </summary>
public class NarrativeWriterTests
{
    private static readonly ReportPeriod Period =
        new(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 24), 24, ReportBucketUnit.Day);

    private static NarrativeContext Context() => new(
        "Sunset Events",
        Period,
        new SalesReportResponse(Period, "Sunset Events", 10_000m, 200, 50m, 12.4m, 3, 150m, 1.5m, 9_850m, 120, 80, [], []),
        new ForecastBlock(AnalyticsSource.Model, 14, 5_000m, 100, 8.2m, [], []),
        new AnomalyBlock(AnalyticsSource.Model, []),
        new SegmentBlock(AnalyticsSource.Model, "posljednjih 12 mjeseci", 36, []),
        []);

    private static OpenAiCompatibleNarrativeWriter Writer(
        HttpMessageHandler handler, NarrativeOptions? options = null) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/v1/") },
            MsOptions.Create(new InsightsOptions { Narrative = options ?? new NarrativeOptions() }),
            NullLogger<OpenAiCompatibleNarrativeWriter>.Instance);

    private static string Completion(string content) =>
        JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { role = "assistant", content } } },
        });

    [Fact]
    public async Task WriteAsync_OnASuccessfulCompletion_ReturnsTheModelsText()
    {
        var handler = new StubHandler(HttpStatusCode.OK, Completion("Prodaja je stabilna u odabranom periodu."));

        var result = await Writer(handler).WriteAsync(Context());

        result.Should().Be("Prodaja je stabilna u odabranom periodu.");
    }

    [Fact]
    public async Task WriteAsync_PostsToTheChatCompletionsPathWithTheConfiguredModel()
    {
        var handler = new StubHandler(HttpStatusCode.OK, Completion("Sažetak."));

        await Writer(handler, new NarrativeOptions { Model = "llama3.2:3b" }).WriteAsync(Context());

        handler.LastRequestUri.Should().Be("http://localhost:11434/v1/chat/completions");
        handler.LastBody.Should().Contain("llama3.2:3b");
        // Streaming off — one response shape to parse rather than two.
        handler.LastBody.Should().Contain("\"stream\":false");
    }

    /// <summary>Only aggregates the caller can already see on screen may leave the service — no
    /// buyer identity, no e-mail, no ticket or order reference.</summary>
    [Fact]
    public async Task WriteAsync_SendsOnlyAggregatedFiguresAndInstructsBosnian()
    {
        var handler = new StubHandler(HttpStatusCode.OK, Completion("Sažetak."));

        await Writer(handler).WriteAsync(Context());

        handler.LastBody.Should().Contain("bosanskom");
        handler.LastBody.Should().Contain("Ukupan prihod");
        handler.LastBody.Should().NotContain("@");
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task WriteAsync_OnAnErrorStatus_ReturnsNullWithoutThrowing(HttpStatusCode status)
    {
        var result = await Writer(new StubHandler(status, "{}")).WriteAsync(Context());

        result.Should().BeNull();
    }

    [Fact]
    public async Task WriteAsync_WhenTheEndpointIsUnreachable_ReturnsNullWithoutThrowing()
    {
        var result = await Writer(new ThrowingHandler(new HttpRequestException("connection refused")))
            .WriteAsync(Context());

        result.Should().BeNull();
    }

    [Fact]
    public async Task WriteAsync_OnATimeout_ReturnsNullWithoutThrowing()
    {
        // A resilience-pipeline timeout surfaces as a cancelled request that the caller's own token
        // did not ask for — the case most likely to be mishandled as "the caller gave up".
        var result = await Writer(new ThrowingHandler(new TaskCanceledException("timed out")))
            .WriteAsync(Context());

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("<html>502 Bad Gateway</html>")]
    [InlineData("{ not json at all")]
    [InlineData("{}")]
    [InlineData("{\"choices\":[]}")]
    [InlineData("{\"choices\":[{\"message\":{\"content\":\"\"}}]}")]
    [InlineData("{\"choices\":[{\"message\":{\"content\":\"   \"}}]}")]
    public async Task WriteAsync_OnAnUnusableBody_ReturnsNullWithoutThrowing(string body)
    {
        var result = await Writer(new StubHandler(HttpStatusCode.OK, body)).WriteAsync(Context());

        result.Should().BeNull();
    }

    [Fact]
    public async Task WriteAsync_WhenTheModelIgnoresTheLengthInstruction_TruncatesOnAWordBoundary()
    {
        var essay = string.Join(" ", Enumerable.Repeat("prodaja", 400));
        var handler = new StubHandler(HttpStatusCode.OK, Completion(essay));

        var result = await Writer(handler, new NarrativeOptions { MaxCharacters = 120 }).WriteAsync(Context());

        result.Should().NotBeNull();
        result!.Length.Should().BeLessThanOrEqualTo(121);
        result.Should().EndWith("…");
    }

    [Fact]
    public async Task WriteAsync_WhenTheAnswerFitsTheCap_LeavesItUntouched()
    {
        var handler = new StubHandler(HttpStatusCode.OK, Completion("Kratak sažetak."));

        var result = await Writer(handler, new NarrativeOptions { MaxCharacters = 900 }).WriteAsync(Context());

        result.Should().Be("Kratak sažetak.");
    }

    [Fact]
    public async Task NullNarrativeWriter_ReturnsNullWithoutTouchingTheNetwork()
    {
        var result = await new NullNarrativeWriter().WriteAsync(Context());

        result.Should().BeNull();
    }

    // ── Doubles ──────────────────────────────────────────────────────────────────────────────

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? LastRequestUri { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequestUri = request.RequestUri?.ToString();
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => throw exception;
    }
}
