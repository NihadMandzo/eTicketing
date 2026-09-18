using System.Security.Claims;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.Analytics;
using eTicketing.Ticketing.Business.Analytics.Narrative;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;
using Moq;

namespace eTicketing.Ticketing.Business.Tests.Analytics;

/// <summary>
/// The orchestration layer: who may see the tab, whose data they get, and that a failure in the one
/// optional part (the LLM) never costs the caller the report.
///
/// Seeded through the real Sqlite in-memory DbContext and the real repositories, so
/// GetBuyerFactsAsync's GROUP BY — the one new SQL projection this feature adds — is exercised for
/// real rather than mocked away.
/// </summary>
public class AnalyticsServiceTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();

    private readonly Guid _orgA = Guid.NewGuid();
    private readonly Guid _orgB = Guid.NewGuid();
    private readonly Guid _productA = Guid.NewGuid();
    private readonly Guid _productB = Guid.NewGuid();

    private readonly Guid _sectorA;
    private readonly Guid _sectorB;

    /// <summary>The fixture's clock is pinned to 24 August 2026, so every range below is in the
    /// past and clears the "not in the future" rule without each test having to say so.</summary>
    private static readonly DateOnly RangeFrom = new(2026, 6, 1);
    private static readonly DateOnly RangeTo = new(2026, 8, 24);

    public AnalyticsServiceTests()
    {
        _fixture.CatalogClient
            .Setup(c => c.GetProductsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _fixture.SeedOrganizationSnapshotAsync(_orgA, "Sunset Events", "Mostar").GetAwaiter().GetResult();

        _sectorA = SeedSector(_orgA, _productA);
        _sectorB = SeedSector(_orgB, _productB);
    }

    // ── Seeding ──────────────────────────────────────────────────────────────────────────────

    private Guid SeedSector(Guid organizationId, Guid productId)
    {
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            OrganizationId = organizationId,
            Name = "Sektor",
            Capacity = 500,
            Price = 50,
            Status = PublishStatus.Published,
            TicketingMode = TicketingMode.SingleOccurrence,
        };

        _fixture.DbContext.Set<Sector>().Add(sector);
        _fixture.DbContext.SaveChanges();
        return sector.Id;
    }

    /// <summary>Two-step save: the audit interceptor stamps CreatedAt on insert and leaves it alone
    /// on update, which is the only seam a test has for placing a sale on a specific day.</summary>
    private void SeedTicket(Guid sectorId, Guid productId, decimal price, DateTime createdAtUtc, Guid? buyerId = null, Guid? orderId = null)
    {
        var ticket = Ticket.ForSingleOccurrence(
            sectorId, null, orderId ?? Guid.NewGuid(), productId, buyerId ?? Guid.NewGuid(), "kupac@test.ba", price);

        _fixture.DbContext.Set<Ticket>().Add(ticket);
        _fixture.DbContext.SaveChanges();

        ticket.CreatedAt = createdAtUtc;
        _fixture.DbContext.SaveChanges();
    }

    /// <summary>A full season of daily sales for one organization — enough history for the model
    /// rung, so the tests below assert on the interesting case rather than the degraded one.</summary>
    private void SeedSeason(Guid sectorId, Guid productId, int days = 80, decimal price = 50m)
    {
        for (var i = 0; i < days; i++)
        {
            var day = RangeTo.AddDays(-(days - 1 - i));
            SeedTicket(sectorId, productId, price, day.ToDateTime(new TimeOnly(12, 0)).ToUniversalTime());
        }
    }

    private static ClaimsPrincipal Caller(string role, Guid? organizationId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role),
        };
        if (organizationId is not null)
            claims.Add(new Claim("organizationId", organizationId.Value.ToString()));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private static InsightsQuery Query(int horizon = 30) => new() { From = RangeFrom, To = RangeTo, Horizon = horizon };

    // ── Role matrix ──────────────────────────────────────────────────────────────────────────

    /// <summary>Insights is a revenue report at heart, so it sits with Prodaja on the matrix and
    /// Admin — whose remit is operational — is refused exactly as it is there.</summary>
    [Fact]
    public async Task GetInsightsAsync_ForAdmin_ReturnsForbidden()
    {
        var result = await _fixture.CreateAnalyticsService().GetInsightsAsync(Query(), Caller("Admin"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("report.forbidden");
    }

    [Fact]
    public async Task GetInsightsAsync_ForACustomer_ReturnsForbidden()
    {
        var result = await _fixture.CreateAnalyticsService().GetInsightsAsync(Query(), Caller("User"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("report.forbidden");
    }

    [Fact]
    public async Task GetInsightsAsync_ForAnOrgRoleWithNoOrganizationClaim_ReturnsNoOrganization()
    {
        var result = await _fixture.CreateAnalyticsService()
            .GetInsightsAsync(Query(), Caller("OrganizationSuperAdmin"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("report.no_organization");
    }

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("OrganizationSuperAdmin")]
    [InlineData("OrganizationAdmin")]
    public async Task GetInsightsAsync_ForAnAllowedRole_Succeeds(string role)
    {
        SeedSeason(_sectorA, _productA);

        var result = await _fixture.CreateAnalyticsService()
            .GetInsightsAsync(Query(), Caller(role, role == "SuperAdmin" ? null : _orgA));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Insights.Should().NotBeEmpty();
    }

    // ── Scoping ──────────────────────────────────────────────────────────────────────────────

    /// <summary>The organization comes from the token, never the request — an organizer must not be
    /// able to see another organization's forecast by widening a range.</summary>
    [Fact]
    public async Task GetInsightsAsync_ForAnOrganizer_ForecastsOnlyItsOwnOrganizationsSales()
    {
        SeedSeason(_sectorA, _productA, price: 50m);
        SeedSeason(_sectorB, _productB, price: 500m);

        var mine = await _fixture.CreateAnalyticsService()
            .GetInsightsAsync(Query(), Caller("OrganizationSuperAdmin", _orgA));
        var platform = await _fixture.CreateAnalyticsService()
            .GetInsightsAsync(Query(), Caller("SuperAdmin"));

        mine.IsSuccess.Should().BeTrue();
        platform.IsSuccess.Should().BeTrue();

        // Organization B sells ten times as much, so a platform-wide projection must be visibly
        // larger than organization A's own.
        platform.Value!.Forecast.ProjectedRevenue.Should().BeGreaterThan(mine.Value!.Forecast.ProjectedRevenue);
    }

    [Fact]
    public async Task GetInsightsAsync_ForAnOrganizer_ScopesSegmentationToItsOwnBuyers()
    {
        var buyer = Guid.NewGuid();
        SeedTicket(_sectorA, _productA, 50m, new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc), buyer);
        SeedTicket(_sectorB, _productB, 50m, new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc));
        SeedTicket(_sectorB, _productB, 50m, new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Utc));

        var result = await _fixture.CreateAnalyticsService()
            .GetInsightsAsync(Query(), Caller("OrganizationSuperAdmin", _orgA));

        result.Value!.Segments.TotalBuyers.Should().Be(1);
    }

    /// <summary>Tickets grouped into one order are one purchase occasion, not three — otherwise a
    /// party-buyer looks like a frequent customer to the segmenter.</summary>
    [Fact]
    public async Task GetInsightsAsync_CountsOneMultiTicketOrderAsASingleBuyer()
    {
        var buyer = Guid.NewGuid();
        var order = Guid.NewGuid();
        for (var i = 0; i < 3; i++)
            SeedTicket(_sectorA, _productA, 50m, new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc), buyer, order);

        var result = await _fixture.CreateAnalyticsService()
            .GetInsightsAsync(Query(), Caller("OrganizationSuperAdmin", _orgA));

        result.Value!.Segments.TotalBuyers.Should().Be(1);
    }

    // ── Shape ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The horizon comes back as asked for, and the drawn series is folded into around a dozen
    /// points whichever horizon that is — weeks for a month or a quarter, fortnights and months
    /// beyond (see SsaSalesForecasterTests for the folding rules).
    /// </summary>
    [Theory]
    [InlineData(30, 5)]
    [InlineData(90, 13)]
    [InlineData(180, 13)]
    [InlineData(365, 13)]
    public async Task GetInsightsAsync_ReturnsTheRequestedHorizon(int horizon, int expectedPoints)
    {
        SeedSeason(_sectorA, _productA);

        var result = await _fixture.CreateAnalyticsService()
            .GetInsightsAsync(Query(horizon), Caller("SuperAdmin"));

        result.Value!.Forecast.Horizon.Should().Be(horizon);
        result.Value.Forecast.Points.Should().HaveCount(expectedPoints);
    }

    [Fact]
    public async Task GetInsightsAsync_EchoesThePeriodAndScopeItActuallyUsed()
    {
        SeedSeason(_sectorA, _productA);

        var result = await _fixture.CreateAnalyticsService()
            .GetInsightsAsync(Query(), Caller("OrganizationSuperAdmin", _orgA));

        result.Value!.Period.From.Should().Be(RangeFrom);
        result.Value.Period.To.Should().Be(RangeTo);
        result.Value.Scope.Should().Be("Sunset Events");
    }

    /// <summary>Segmentation deliberately ignores the selected range in favour of a trailing year,
    /// and the response has to say so or the mismatch reads as a bug.</summary>
    [Fact]
    public async Task GetInsightsAsync_LabelsTheSegmentationWindowItActuallyUsed()
    {
        SeedSeason(_sectorA, _productA);

        var result = await _fixture.CreateAnalyticsService().GetInsightsAsync(Query(), Caller("SuperAdmin"));

        result.Value!.Segments.WindowLabel.Should().Be("posljednjih 12 mjeseci");
    }

    [Fact]
    public async Task GetInsightsAsync_ForARangeWithNoSales_SucceedsWithAnExplanatoryCard()
    {
        var result = await _fixture.CreateAnalyticsService().GetInsightsAsync(Query(), Caller("SuperAdmin"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Insights.Should().ContainSingle();
        result.Value.Forecast.Source.Should().Be(AnalyticsSource.Insufficient);
        result.Value.Segments.Source.Should().Be(AnalyticsSource.Insufficient);
    }

    // ── Narrative ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetInsightsAsync_WithNoNarrativeWriterConfigured_ReturnsEverythingElseWithoutASummary()
    {
        SeedSeason(_sectorA, _productA);

        var result = await _fixture.CreateAnalyticsService().GetInsightsAsync(Query(), Caller("SuperAdmin"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Narrative.Should().BeNull();
        result.Value.Insights.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetInsightsAsync_WithAWorkingNarrativeWriter_IncludesTheSummary()
    {
        SeedSeason(_sectorA, _productA);

        var result = await _fixture.CreateAnalyticsService(new StubNarrativeWriter("Prodaja je stabilna."))
            .GetInsightsAsync(Query(), Caller("SuperAdmin"));

        result.Value!.Narrative.Should().Be("Prodaja je stabilna.");
    }

    /// <summary>The guarantee the whole optional-narrative design exists for: the deterministic
    /// insights are already computed by the time the model is asked, and throwing them away over a
    /// dead LLM would be the worst possible trade.</summary>
    [Fact]
    public async Task GetInsightsAsync_WhenTheNarrativeWriterThrows_StillReturnsTheReport()
    {
        SeedSeason(_sectorA, _productA);

        var result = await _fixture.CreateAnalyticsService(new ThrowingNarrativeWriter())
            .GetInsightsAsync(Query(), Caller("SuperAdmin"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Narrative.Should().BeNull();
        result.Value.Insights.Should().NotBeEmpty();
        result.Value.Forecast.Points.Should().NotBeEmpty();
    }

    // ── Caching ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetInsightsAsync_CalledTwiceOnTheSameRange_ServesTheSecondFromCache()
    {
        SeedSeason(_sectorA, _productA);

        var writer = new CountingNarrativeWriter();
        var sut = _fixture.CreateAnalyticsService(writer);

        await sut.GetInsightsAsync(Query(), Caller("SuperAdmin"));
        await sut.GetInsightsAsync(Query(), Caller("SuperAdmin"));

        writer.Calls.Should().Be(1);
    }

    [Fact]
    public async Task GetInsightsAsync_WithADifferentHorizon_DoesNotServeTheCachedResponse()
    {
        SeedSeason(_sectorA, _productA);

        var writer = new CountingNarrativeWriter();
        var sut = _fixture.CreateAnalyticsService(writer);

        await sut.GetInsightsAsync(Query(horizon: 30), Caller("SuperAdmin"));
        await sut.GetInsightsAsync(Query(horizon: 90), Caller("SuperAdmin"));

        writer.Calls.Should().Be(2);
    }

    /// <summary>An OrganizationAdmin's response legitimately omits the gate statistics an
    /// OrganizationSuperAdmin of the same organization sees, so the two must never share an entry.
    /// A cache key that ignored the role would be an authorization bug in disguise.</summary>
    [Fact]
    public async Task GetInsightsAsync_ForTwoRolesInTheSameOrganization_DoesNotShareACacheEntry()
    {
        SeedSeason(_sectorA, _productA);

        var writer = new CountingNarrativeWriter();
        var sut = _fixture.CreateAnalyticsService(writer);

        await sut.GetInsightsAsync(Query(), Caller("OrganizationSuperAdmin", _orgA));
        await sut.GetInsightsAsync(Query(), Caller("OrganizationAdmin", _orgA));

        writer.Calls.Should().Be(2);
    }

    /// <summary>
    /// Two organizations, the same role, the same range — and no shared cache entry.
    ///
    /// The scoping itself is asserted above (ForecastsOnlyItsOwnOrganizationsSales); this is the
    /// other half of the same guarantee, and the half that fails quietly. A cache key that dropped
    /// the organization would hand the second caller the first organization's revenue with every
    /// query still perfectly scoped, and nothing in the response would look wrong.
    /// </summary>
    [Fact]
    public async Task GetInsightsAsync_ForTwoOrganizations_NeverServesOneTheOthersFigures()
    {
        // Different prices, so the two organizations cannot produce the same number by accident.
        SeedSeason(_sectorA, _productA, price: 50m);
        SeedSeason(_sectorB, _productB, price: 130m);

        var sut = _fixture.CreateAnalyticsService();

        var first = await sut.GetInsightsAsync(Query(), Caller("OrganizationSuperAdmin", _orgA));
        var second = await sut.GetInsightsAsync(Query(), Caller("OrganizationSuperAdmin", _orgB));

        first.Value!.Forecast.ProjectedRevenue.Should().BeGreaterThan(0);
        second.Value!.Forecast.ProjectedRevenue.Should().BeGreaterThan(0);
        second.Value.Forecast.ProjectedRevenue.Should().NotBe(first.Value.Forecast.ProjectedRevenue);
    }

    /// <summary>The organization is read from the caller's own token, never from the request, so
    /// there is no parameter for an organizer to point at somebody else's data. This pins the claim
    /// that would have to change for that to stop being true.</summary>
    [Fact]
    public async Task GetInsightsAsync_ForAnOrganizer_IgnoresEveryOrganizationButItsOwn()
    {
        SeedSeason(_sectorB, _productB, price: 130m);

        var result = await _fixture.CreateAnalyticsService()
            .GetInsightsAsync(Query(), Caller("OrganizationSuperAdmin", _orgA));

        // Organization A sold nothing in this range; B sold every day of it.
        result.Value!.Forecast.ProjectedRevenue.Should().Be(0);
        result.Value.Segments.TotalBuyers.Should().Be(0);
    }

    public void Dispose() => _fixture.Dispose();

    // ── Doubles ──────────────────────────────────────────────────────────────────────────────

    private sealed class StubNarrativeWriter(string text) : INarrativeWriter
    {
        public Task<string?> WriteAsync(NarrativeContext context, CancellationToken ct = default)
            => Task.FromResult<string?>(text);
    }

    private sealed class ThrowingNarrativeWriter : INarrativeWriter
    {
        public Task<string?> WriteAsync(NarrativeContext context, CancellationToken ct = default)
            => throw new HttpRequestException("model server unreachable");
    }

    /// <summary>Stands in for "was the response recomputed" — the narrative writer is the last step
    /// of BuildAsync, so a call to it means the cache was missed.</summary>
    private sealed class CountingNarrativeWriter : INarrativeWriter
    {
        public int Calls { get; private set; }

        public Task<string?> WriteAsync(NarrativeContext context, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult<string?>("sažetak");
        }
    }
}
