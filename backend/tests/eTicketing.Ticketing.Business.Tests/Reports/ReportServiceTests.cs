using System.Security.Claims;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Reports;
using eTicketing.Ticketing.Business.Tests.TestFixtures;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;
using Moq;

namespace eTicketing.Ticketing.Business.Tests.Reports;

/// <summary>
/// Covers the three things ReportService actually decides: who may see which report, which rows
/// fall inside the requested window, and how the raw tallies become the figures on screen.
///
/// Everything is seeded through the real Sqlite in-memory DbContext and the real repositories, so
/// the GROUP BY projections in TicketRepository are exercised for real rather than mocked away —
/// they are the half of this feature most likely to break silently.
/// </summary>
public class ReportServiceTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();
    private readonly IReportService _sut;

    private readonly Guid _orgA = Guid.NewGuid();
    private readonly Guid _orgB = Guid.NewGuid();
    private readonly Guid _productA = Guid.NewGuid();
    private readonly Guid _productB = Guid.NewGuid();

    private Guid _sectorA;
    private Guid _sectorB;

    /// <summary>The fixture's clock is pinned to 24 August 2026, so every range below sits in the
    /// past and passes the "not in the future" rule without the test having to say so.</summary>
    private static readonly DateOnly RangeFrom = new(2026, 8, 1);
    private static readonly DateOnly RangeTo = new(2026, 8, 24);

    public ReportServiceTests()
    {
        _sut = _fixture.CreateReportService();

        // Catch-all first: a report over a period with no sales asks Catalog for an empty id list,
        // which none of the per-product setups below match — and an unmatched Moq call returns
        // null, not an empty list, which would blow up in the service rather than in the assert.
        _fixture.CatalogClient
            .Setup(c => c.GetProductsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _sectorA = SeedSector(_orgA, _productA, capacity: 100, price: 50);
        _sectorB = SeedSector(_orgB, _productB, capacity: 40, price: 25);

        MockCatalogProduct(_productA, _orgA, "Ljetni Festival", TicketingMode.SingleOccurrence);
        MockCatalogProduct(_productB, _orgB, "Jazz Noći", TicketingMode.SingleOccurrence);
    }

    // ── Seeding helpers ──────────────────────────────────────────────────────────────────────

    private Guid SeedSector(Guid organizationId, Guid productId, int capacity, decimal price,
        PublishStatus status = PublishStatus.Published, TicketingMode mode = TicketingMode.SingleOccurrence)
    {
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            OrganizationId = organizationId,
            Name = "Sektor",
            Capacity = capacity,
            Price = price,
            Status = status,
            TicketingMode = mode,
        };

        _fixture.DbContext.Set<Sector>().Add(sector);
        _fixture.DbContext.SaveChanges();
        return sector.Id;
    }

    /// <summary>
    /// Inserts one ticket and then backdates it.
    ///
    /// The two-step save is required, not incidental: AuditableEntitySaveChangesInterceptor stamps
    /// CreatedAt on every Added entity and is the only thing allowed to set it, so a ticket cannot
    /// be born in the past. It leaves CreatedAt alone on Modified entities, which is the seam these
    /// tests use to place a sale on a specific day.
    /// </summary>
    private Ticket SeedTicket(
        Guid sectorId, Guid productId, decimal price, DateTime createdAtUtc,
        TicketStatus status = TicketStatus.Confirmed,
        TicketOrigin origin = TicketOrigin.Online,
        DateTime? validatedAtUtc = null)
    {
        var ticket = origin == TicketOrigin.Printed
            // A printed ticket carries a real PrintBatchId FK and a serial that is unique per
            // product, so a batch row has to exist first and the serial has to keep climbing —
            // the same two constraints the real export path satisfies.
            ? Ticket.ForPrint(sectorId, null, SeedPrintBatch(productId), productId, price, ++_serialNumber, validDate: null)
            : Ticket.ForSingleOccurrence(sectorId, null, Guid.NewGuid(), productId, Guid.NewGuid(), "kupac@test.ba", price);

        _fixture.DbContext.Set<Ticket>().Add(ticket);
        _fixture.DbContext.SaveChanges();

        if (validatedAtUtc is not null)
            ticket.MarkValidated(Guid.NewGuid(), validatedAtUtc.Value);
        else
            ticket.Status = status;

        ticket.CreatedAt = createdAtUtc;
        _fixture.DbContext.SaveChanges();

        return ticket;
    }

    /// <summary>Serial numbers are unique per product and never restart, so one running counter
    /// across the whole test class keeps every seeded printed ticket clear of the unique index.</summary>
    private int _serialNumber;

    private Guid SeedPrintBatch(Guid productId)
    {
        var batch = new TicketPrintBatch
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            OrganizationId = _orgA,
            RequestedByUserId = Guid.NewGuid(),
            ProductName = "Ljetni Festival",
            Status = TicketPrintBatchStatus.Ready,
            TicketCount = 1,
            SerialFrom = _serialNumber + 1,
            SerialTo = _serialNumber + 1,
            NominalValue = 50,
        };

        _fixture.DbContext.Set<TicketPrintBatch>().Add(batch);
        _fixture.DbContext.SaveChanges();
        return batch.Id;
    }

    private void MockCatalogProduct(Guid productId, Guid organizationId, string name, TicketingMode mode) =>
        _fixture.CatalogClient
            .Setup(c => c.GetProductsAsync(It.Is<IReadOnlyList<Guid>>(ids => ids.Contains(productId)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CatalogProductResponse>
            {
                new(productId, organizationId, PublishStatus.Published, mode, name, null, City.Mostar),
            });

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

    private static ReportQuery Range(DateOnly? from = null, DateOnly? to = null) =>
        new() { From = from ?? RangeFrom, To = to ?? RangeTo };

    // ── Role / tab matrix ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSalesAsync_ForAdmin_ReturnsForbidden()
    {
        var result = await _sut.GetSalesAsync(Range(), Caller("Admin"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("report.forbidden");
    }

    [Fact]
    public async Task GetRedemptionAsync_ForAdmin_ReturnsForbidden()
    {
        var result = await _sut.GetRedemptionAsync(Range(), Caller("Admin"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("report.forbidden");
    }

    [Fact]
    public async Task GetRedemptionAsync_ForOrganizationAdmin_ReturnsForbidden()
    {
        var result = await _sut.GetRedemptionAsync(Range(), Caller("OrganizationAdmin", _orgA));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("report.forbidden");
    }

    [Theory]
    [InlineData("OrganizationSuperAdmin")]
    [InlineData("OrganizationAdmin")]
    public async Task GetOrganizationsAsync_ForOrganizerRoles_ReturnsForbidden(string role)
    {
        var result = await _sut.GetOrganizationsAsync(Range(), Caller(role, _orgA));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("report.forbidden");
    }

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("Admin")]
    [InlineData("OrganizationSuperAdmin")]
    [InlineData("OrganizationAdmin")]
    public async Task GetProductsAsync_ForEveryStaffRole_IsAllowed(string role)
    {
        var organizationId = role.StartsWith("Organization") ? _orgA : (Guid?)null;

        var result = await _sut.GetProductsAsync(Range(), Caller(role, organizationId));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetSalesAsync_ForOrganizerWithoutOrganizationClaim_ReturnsForbidden()
    {
        // A malformed session, not a permission question: it must never fall through to the
        // platform-wide scope.
        var result = await _sut.GetSalesAsync(Range(), Caller("OrganizationSuperAdmin"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("report.no_organization");
    }

    [Fact]
    public async Task GetSalesAsync_ForCustomerRole_ReturnsForbidden()
    {
        var result = await _sut.GetSalesAsync(Range(), Caller("User"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("report.forbidden");
    }

    // ── Scoping ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSalesAsync_ForOrganizer_CountsOnlyItsOwnOrganizationsTickets()
    {
        SeedTicket(_sectorA, _productA, 50m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc));
        SeedTicket(_sectorB, _productB, 25m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc));

        var result = await _sut.GetSalesAsync(Range(), Caller("OrganizationSuperAdmin", _orgA));

        result.IsSuccess.Should().BeTrue();
        result.Value!.TicketsSold.Should().Be(1);
        result.Value.GrossRevenue.Should().Be(50m);
    }

    [Fact]
    public async Task GetSalesAsync_ForSuperAdmin_CountsEveryOrganizationsTickets()
    {
        SeedTicket(_sectorA, _productA, 50m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc));
        SeedTicket(_sectorB, _productB, 25m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc));

        var result = await _sut.GetSalesAsync(Range(), Caller("SuperAdmin"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.TicketsSold.Should().Be(2);
        result.Value.GrossRevenue.Should().Be(75m);
        result.Value.Scope.Should().Be("Platforma");
    }

    // ── Prodaja po organizacijama ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSalesAsync_ForSuperAdmin_SplitsRevenueByOrganizationHighestFirst()
    {
        _fixture.IdentityClient
            .Setup(c => c.GetOrganizationsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new IdentityOrganizationResponse(_orgA, "Sunset Events", "Mostar", IsActive: true),
                new IdentityOrganizationResponse(_orgB, "Vardar Sport", "Skopje", IsActive: true),
            ]);
        SeedTicket(_sectorA, _productA, 50m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc));
        SeedTicket(_sectorA, _productA, 50m, new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc));
        SeedTicket(_sectorB, _productB, 25m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc));

        var result = await _sut.GetSalesAsync(Range(), Caller("SuperAdmin"));

        result.IsSuccess.Should().BeTrue();
        var rows = result.Value!.ByOrganization;
        rows.Should().HaveCount(2);

        rows[0].OrganizationId.Should().Be(_orgA);
        rows[0].Name.Should().Be("Sunset Events");
        rows[0].Sold.Should().Be(2);
        rows[0].Revenue.Should().Be(100m);
        rows[0].AveragePrice.Should().Be(50m);

        rows[1].OrganizationId.Should().Be(_orgB);
        rows[1].Revenue.Should().Be(25m);

        // The whole point of the section: it must account for the headline figure exactly.
        rows.Sum(r => r.Revenue).Should().Be(result.Value.GrossRevenue);
        rows.Sum(r => r.Sold).Should().Be(result.Value.TicketsSold);
        rows.Sum(r => r.SharePercent).Should().Be(100m);
    }

    [Theory]
    [InlineData("OrganizationSuperAdmin")]
    [InlineData("OrganizationAdmin")]
    public async Task GetSalesAsync_ForAnOrganizer_ReturnsNoOrganizationBreakdown(string role)
    {
        // Their report is already one organization's, so a breakdown would be a single row
        // restating the totals above it — and it would name their own organization back to them.
        SeedTicket(_sectorA, _productA, 50m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc));

        var result = await _sut.GetSalesAsync(Range(), Caller(role, _orgA));

        result.IsSuccess.Should().BeTrue();
        result.Value!.ByOrganization.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSalesAsync_WithNoSalesInRange_ReturnsEmptyOrganizationBreakdown()
    {
        var result = await _sut.GetSalesAsync(Range(), Caller("SuperAdmin"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.ByOrganization.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSalesAsync_ForAnOrganizationIdentityCannotName_StillReportsItsSales()
    {
        // The fixture's IdentityClient answers with an empty list by default. Dropping the row
        // would silently unbalance the breakdown against the headline total, so it is kept under
        // a placeholder name — same fallback as the Organizacije tab.
        SeedTicket(_sectorA, _productA, 40m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc));

        var result = await _sut.GetSalesAsync(Range(), Caller("SuperAdmin"));

        result.Value!.ByOrganization.Should().ContainSingle()
            .Which.Name.Should().Be("Nepoznata organizacija");
        result.Value.ByOrganization.Sum(r => r.Revenue).Should().Be(result.Value.GrossRevenue);
    }

    [Fact]
    public async Task GetSalesAsync_ExcludesOrganizationsWithSalesOnlyOutsideTheRange()
    {
        SeedTicket(_sectorA, _productA, 50m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc));
        SeedTicket(_sectorB, _productB, 25m, new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc));

        var result = await _sut.GetSalesAsync(Range(), Caller("SuperAdmin"));

        result.Value!.ByOrganization.Should().ContainSingle()
            .Which.OrganizationId.Should().Be(_orgA);
    }

    // ── Window ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSalesAsync_ExcludesTicketsOutsideTheRange()
    {
        SeedTicket(_sectorA, _productA, 50m, new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc));
        SeedTicket(_sectorA, _productA, 50m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc));

        var result = await _sut.GetSalesAsync(Range(), Caller("SuperAdmin"));

        result.Value!.TicketsSold.Should().Be(1);
    }

    [Fact]
    public async Task GetSalesAsync_IncludesTicketsSoldOnTheLastDayOfTheRange()
    {
        // The upper bound is exclusive-at-next-midnight precisely so a late sale on the final day
        // still lands inside the report.
        SeedTicket(_sectorA, _productA, 50m, new DateTime(2026, 8, 24, 19, 45, 0, DateTimeKind.Utc));

        var result = await _sut.GetSalesAsync(Range(), Caller("SuperAdmin"));

        result.Value!.TicketsSold.Should().Be(1);
    }

    [Fact]
    public async Task GetSalesAsync_ForRangeWithNoSales_ReturnsZeroesRatherThanFailing()
    {
        var result = await _sut.GetSalesAsync(Range(), Caller("SuperAdmin"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.GrossRevenue.Should().Be(0m);
        result.Value.TicketsSold.Should().Be(0);
        result.Value.AverageTicketPrice.Should().Be(0m);
        result.Value.CancellationRatePercent.Should().Be(0m);
        result.Value.Buckets.Should().NotBeEmpty("an empty period still has days to draw");
    }

    // ── Figures ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSalesAsync_KeepsCancelledTicketsOutOfRevenueButReportsThemSeparately()
    {
        SeedTicket(_sectorA, _productA, 100m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc));
        SeedTicket(_sectorA, _productA, 100m, new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc),
            status: TicketStatus.Cancelled);

        var result = await _sut.GetSalesAsync(Range(), Caller("SuperAdmin"));

        result.Value!.GrossRevenue.Should().Be(100m);
        result.Value.TicketsSold.Should().Be(1);
        result.Value.CancelledCount.Should().Be(1);
        result.Value.CancelledAmount.Should().Be(100m);
        result.Value.NetRevenue.Should().Be(0m);
        result.Value.CancellationRatePercent.Should().Be(50m);
    }

    [Fact]
    public async Task GetSalesAsync_IgnoresProcessingTickets()
    {
        // Processing never completed — it is neither revenue nor a cancellation.
        SeedTicket(_sectorA, _productA, 100m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc),
            status: TicketStatus.Processing);

        var result = await _sut.GetSalesAsync(Range(), Caller("SuperAdmin"));

        result.Value!.TicketsSold.Should().Be(0);
        result.Value.CancelledCount.Should().Be(0);
    }

    [Fact]
    public async Task GetSalesAsync_SplitsSoldTicketsByOrigin()
    {
        SeedTicket(_sectorA, _productA, 100m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc),
            origin: TicketOrigin.Online);
        SeedTicket(_sectorA, _productA, 100m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc),
            origin: TicketOrigin.Online);
        SeedTicket(_sectorA, _productA, 100m, new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc),
            origin: TicketOrigin.Printed);

        var result = await _sut.GetSalesAsync(Range(), Caller("SuperAdmin"));

        result.Value!.TicketsSold.Should().Be(3);
        result.Value.OnlineSold.Should().Be(2);
        result.Value.PrintedSold.Should().Be(1);
        (result.Value.OnlineSold + result.Value.PrintedSold).Should().Be(result.Value.TicketsSold);
    }

    [Fact]
    public async Task GetSalesAsync_ExcludesCancelledAndProcessingTicketsFromTheOriginSplit()
    {
        SeedTicket(_sectorA, _productA, 100m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc),
            status: TicketStatus.Cancelled, origin: TicketOrigin.Online);
        SeedTicket(_sectorA, _productA, 100m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc),
            status: TicketStatus.Processing, origin: TicketOrigin.Printed);

        var result = await _sut.GetSalesAsync(Range(), Caller("SuperAdmin"));

        result.Value!.OnlineSold.Should().Be(0);
        result.Value.PrintedSold.Should().Be(0);
    }

    [Fact]
    public async Task GetSalesAsync_ComparesAgainstTheImmediatelyPrecedingEqualLengthPeriod()
    {
        // 1-10 August is a 10-day range, so the baseline is 22-31 July.
        SeedTicket(_sectorA, _productA, 100m, new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc));
        SeedTicket(_sectorA, _productA, 150m, new DateTime(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc));

        var result = await _sut.GetSalesAsync(Range(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 10)), Caller("SuperAdmin"));

        result.Value!.GrossRevenue.Should().Be(150m);
        result.Value.RevenueChangePercent.Should().Be(50m);
    }

    [Fact]
    public async Task GetSalesAsync_WhenThePrecedingPeriodSoldNothing_ReportsNoChangeRatherThanInfinity()
    {
        SeedTicket(_sectorA, _productA, 150m, new DateTime(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc));

        var result = await _sut.GetSalesAsync(Range(), Caller("SuperAdmin"));

        result.Value!.RevenueChangePercent.Should().BeNull();
    }

    [Fact]
    public async Task GetSalesAsync_PlacesASaleJustAfterLocalMidnightInThatLocalDaysBucket()
    {
        // 2026-08-17T22:05Z is 2026-08-18 00:05 in Sarajevo — the first few minutes of the range.
        // Keyed by UTC day it would land on the 17th, a day the bucket cursor never visits, and
        // vanish from the chart while still counting toward the headline total.
        SeedTicket(_sectorA, _productA, 90m, new DateTime(2026, 8, 17, 22, 5, 0, DateTimeKind.Utc));

        var result = await _sut.GetSalesAsync(
            Range(new DateOnly(2026, 8, 18), new DateOnly(2026, 8, 24)), Caller("SuperAdmin"));

        var buckets = result.Value!.Buckets;
        buckets.Should().HaveCount(7);
        buckets[0].Label.Should().Be("18. avg");
        buckets[0].Revenue.Should().Be(90m, "the sale happened just after local midnight on the 18th");
        buckets[0].Sold.Should().Be(1);
    }

    [Fact]
    public async Task GetSalesAsync_ExcludesASaleJustBeforeLocalMidnightOnTheDayBeforeTheRange()
    {
        // The mirror of the test above: 21:55Z on the 17th is still 23:55 local on the 17th, one
        // day before the range starts, so it must not appear at all.
        SeedTicket(_sectorA, _productA, 90m, new DateTime(2026, 8, 17, 21, 55, 0, DateTimeKind.Utc));

        var result = await _sut.GetSalesAsync(
            Range(new DateOnly(2026, 8, 18), new DateOnly(2026, 8, 24)), Caller("SuperAdmin"));

        result.Value!.GrossRevenue.Should().Be(0m);
        result.Value.Buckets.Sum(b => b.Revenue).Should().Be(0m);
    }

    [Fact]
    public async Task GetSalesAsync_BucketRevenueAlwaysSumsToTheHeadlineTotal()
    {
        // The invariant the UTC/local mismatch broke: the chart and the tile above it are two
        // views of one list, so they cannot disagree. Seeded across both midnight boundaries and
        // the middle of the range.
        SeedTicket(_sectorA, _productA, 90m, new DateTime(2026, 8, 17, 22, 5, 0, DateTimeKind.Utc));
        SeedTicket(_sectorA, _productA, 40m, new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc));
        SeedTicket(_sectorA, _productA, 25m, new DateTime(2026, 8, 24, 21, 30, 0, DateTimeKind.Utc));

        var result = await _sut.GetSalesAsync(
            Range(new DateOnly(2026, 8, 18), new DateOnly(2026, 8, 24)), Caller("SuperAdmin"));

        result.Value!.Buckets.Sum(b => b.Revenue).Should().Be(result.Value.GrossRevenue);
        result.Value.Buckets.Sum(b => b.Sold).Should().Be(result.Value.TicketsSold);
    }

    [Theory]
    // The bucket unit is picked from the range length: daily bars up to a fortnight, weekly up to
    // a quarter, monthly beyond. These are the exact boundaries.
    [InlineData(14, ReportBucketUnit.Day)]
    [InlineData(15, ReportBucketUnit.Week)]
    [InlineData(92, ReportBucketUnit.Week)]
    [InlineData(93, ReportBucketUnit.Month)]
    public async Task GetSalesAsync_PicksTheBucketUnitFromTheRangeLength(int days, ReportBucketUnit expected)
    {
        var to = new DateOnly(2026, 8, 24);
        var from = to.AddDays(-(days - 1));

        var result = await _sut.GetSalesAsync(Range(from, to), Caller("SuperAdmin"));

        result.Value!.Period.Days.Should().Be(days);
        result.Value.Period.BucketUnit.Should().Be(expected);
    }

    [Fact]
    public async Task GetSalesAsync_DrawsOneBucketPerDayForAShortRange()
    {
        var result = await _sut.GetSalesAsync(
            Range(new DateOnly(2026, 8, 18), new DateOnly(2026, 8, 24)), Caller("SuperAdmin"));

        result.Value!.Buckets.Should().HaveCount(7);
        result.Value.Buckets[0].Label.Should().Be("18. avg");
        result.Value.Buckets[^1].Label.Should().Be("24. avg");
    }

    // ── Products ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProductsAsync_ComputesOccupancyAgainstPublishedSectorCapacity()
    {
        // 25 of sector A's 100 seats.
        for (var i = 0; i < 25; i++)
            SeedTicket(_sectorA, _productA, 50m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc));

        var result = await _sut.GetProductsAsync(Range(), Caller("OrganizationSuperAdmin", _orgA));

        var row = result.Value!.Rows.Should().ContainSingle().Subject;
        row.Sold.Should().Be(25);
        row.OccupancyPercent.Should().Be(25m);
        row.AveragePrice.Should().Be(50m);
        row.Revenue.Should().Be(1250m);
    }

    [Fact]
    public async Task GetProductsAsync_ForDailyEntryProduct_ReportsNoOccupancy()
    {
        // A DailyEntry sector's capacity is a per-day allowance across a month, so there is no
        // single denominator to divide by — the row must say "unknown", not guess.
        var dailyProduct = Guid.NewGuid();
        var dailySector = SeedSector(_orgA, dailyProduct, capacity: 300, price: 10, mode: TicketingMode.DailyEntry);
        MockCatalogProduct(dailyProduct, _orgA, "Muzej", TicketingMode.DailyEntry);
        SeedTicket(dailySector, dailyProduct, 10m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc));

        var result = await _sut.GetProductsAsync(Range(), Caller("OrganizationSuperAdmin", _orgA));

        result.Value!.Rows.Should().ContainSingle()
            .Which.OccupancyPercent.Should().BeNull();
    }

    [Fact]
    public async Task GetProductsAsync_IgnoresDraftSectorsInTheOccupancyDenominator()
    {
        // A draft sector was never on sale, so its seats must not depress occupancy.
        SeedSector(_orgA, _productA, capacity: 900, price: 50, status: PublishStatus.Draft);
        for (var i = 0; i < 50; i++)
            SeedTicket(_sectorA, _productA, 50m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc));

        var result = await _sut.GetProductsAsync(Range(), Caller("OrganizationSuperAdmin", _orgA));

        result.Value!.Rows.Should().ContainSingle()
            .Which.OccupancyPercent.Should().Be(50m);
    }

    [Fact]
    public async Task GetProductsAsync_LabelsAProductWhoseCatalogRowIsGone()
    {
        var orphaned = Guid.NewGuid();
        var orphanedSector = SeedSector(_orgA, orphaned, capacity: 10, price: 20);
        _fixture.CatalogClient
            .Setup(c => c.GetProductsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        SeedTicket(orphanedSector, orphaned, 20m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc));

        var result = await _sut.GetProductsAsync(Range(), Caller("OrganizationSuperAdmin", _orgA));

        // The sale still happened, so it stays in the totals rather than silently disappearing.
        var row = result.Value!.Rows.Should().ContainSingle().Subject;
        row.Name.Should().Be("Obrisani proizvod");
        row.Revenue.Should().Be(20m);
    }

    [Fact]
    public async Task GetProductsAsync_WithMoreProductsThanOneLookupAllows_SplitsTheCatalogCallIntoBatches()
    {
        // Catalog and Identity both refuse an id list longer than 200 with a 400, which the HTTP
        // clients turn into an exception rather than a Result — so an unbatched call would surface
        // as an opaque 500 on exactly the platform-wide reports the tab exists for.
        var products = new List<CatalogProductResponse>();
        for (var i = 0; i < 250; i++)
        {
            var productId = Guid.NewGuid();
            var sectorId = SeedSector(_orgA, productId, capacity: 10, price: 5);
            SeedTicket(sectorId, productId, 5m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc));
            products.Add(new CatalogProductResponse(
                productId, _orgA, PublishStatus.Published, TicketingMode.SingleOccurrence,
                $"Proizvod {i}", null, City.Mostar));
        }

        var requestedBatches = new List<int>();
        _fixture.CatalogClient
            .Setup(c => c.GetProductsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Guid> ids, CancellationToken _) =>
            {
                requestedBatches.Add(ids.Count);
                return products.Where(p => ids.Contains(p.Id)).ToList();
            });

        var result = await _sut.GetProductsAsync(Range(), Caller("OrganizationSuperAdmin", _orgA));

        result.IsSuccess.Should().BeTrue();
        // Split at the downstream cap of 200, in order.
        requestedBatches.Should().Equal(new[] { 200, 50 });
        // Every row still resolved a real name — batching must not cost any labels.
        result.Value!.Rows.Should().HaveCount(250);
        result.Value.Rows.Should().NotContain(r => r.Name == "Obrisani proizvod");
    }

    // ── Redemption ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRedemptionAsync_SplitsSoldTicketsIntoCheckedInAndNoShow()
    {
        SeedTicket(_sectorA, _productA, 50m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc),
            validatedAtUtc: new DateTime(2026, 8, 12, 19, 30, 0, DateTimeKind.Utc));
        SeedTicket(_sectorA, _productA, 50m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc));
        SeedTicket(_sectorA, _productA, 50m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc));
        SeedTicket(_sectorA, _productA, 50m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc));

        var result = await _sut.GetRedemptionAsync(Range(), Caller("OrganizationSuperAdmin", _orgA));

        var row = result.Value!.Rows.Should().ContainSingle().Subject;
        row.Sold.Should().Be(4);
        row.CheckedIn.Should().Be(1);
        row.NoShow.Should().Be(3);
        row.RatePercent.Should().Be(25m);
        result.Value.NoShowRatePercent.Should().Be(75m);
    }

    [Fact]
    public async Task GetRedemptionAsync_ReportsThePeakArrivalHourInPlatformLocalTime()
    {
        // Scans stored at 19:xx UTC. August in Europe/Sarajevo is CEST (UTC+2), so the hour the
        // gate staff actually lived through — and the only one worth charting — is 21:00.
        foreach (var minute in new[] { 0, 10, 20 })
        {
            SeedTicket(_sectorA, _productA, 50m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc),
                validatedAtUtc: new DateTime(2026, 8, 12, 19, minute, 0, DateTimeKind.Utc));
        }
        SeedTicket(_sectorA, _productA, 50m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc),
            validatedAtUtc: new DateTime(2026, 8, 12, 21, 5, 0, DateTimeKind.Utc));

        var result = await _sut.GetRedemptionAsync(Range(), Caller("OrganizationSuperAdmin", _orgA));

        result.Value!.PeakHour.Should().Be(21, "19:00 UTC is 21:00 in Sarajevo during summer time");
        result.Value.PeakHourSharePercent.Should().Be(75m);
        result.Value.CheckinsByHour.Should().HaveCount(2);
        result.Value.CheckinsByHour.Select(h => h.Hour).Should().Equal(21, 23);
    }

    [Fact]
    public async Task GetRedemptionAsync_AppliesTheWinterOffsetToArrivalHours()
    {
        // The companion to the test above, and the reason the conversion cannot be a hardcoded
        // +2: January is CET (UTC+1), so the same 19:xx UTC scan is a 20:00 arrival, not 21:00.
        // No need to move the fixture clock — it is pinned to August 2026, so a January range is
        // already in the past and passes the "not in the future" rule as-is.
        SeedTicket(_sectorA, _productA, 50m, new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc),
            validatedAtUtc: new DateTime(2026, 1, 12, 19, 30, 0, DateTimeKind.Utc));

        var result = await _sut.GetRedemptionAsync(
            Range(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 20)),
            Caller("OrganizationSuperAdmin", _orgA));

        result.Value!.PeakHour.Should().Be(20);
    }

    [Fact]
    public async Task GetRedemptionAsync_WithNoScansAtAll_ReportsNoPeakHour()
    {
        SeedTicket(_sectorA, _productA, 50m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc));

        var result = await _sut.GetRedemptionAsync(Range(), Caller("OrganizationSuperAdmin", _orgA));

        result.Value!.PeakHour.Should().BeNull();
        result.Value.PeakHourSharePercent.Should().BeNull();
    }

    [Fact]
    public async Task GetRedemptionAsync_CountsPrintedTicketsSeparatelyFromOnlineOnes()
    {
        SeedTicket(_sectorA, _productA, 50m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc));
        SeedTicket(_sectorA, _productA, 50m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc),
            origin: TicketOrigin.Printed);

        var result = await _sut.GetRedemptionAsync(Range(), Caller("OrganizationSuperAdmin", _orgA));

        result.Value!.PrintedTickets.Should().Be(1);
        result.Value.Rows.Should().ContainSingle().Which.Sold.Should().Be(2);
    }

    // ── Organizations ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrganizationsAsync_ForSuperAdmin_ReturnsTheFinancialColumnSet()
    {
        _fixture.CatalogClient
            .Setup(c => c.GetOrganizationProductStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CatalogOrganizationProductStats(_orgA, Total: 3, Published: 2, Draft: 1, WithoutImage: 1)]);
        _fixture.IdentityClient
            .Setup(c => c.GetOrganizationsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new IdentityOrganizationResponse(_orgA, "Sunset Events", "Mostar", IsActive: true)]);
        SeedTicket(_sectorA, _productA, 80m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc));

        var result = await _sut.GetOrganizationsAsync(Range(), Caller("SuperAdmin"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.View.Should().Be(OrganizationReportView.Financial);

        var row = result.Value.Rows.Should().ContainSingle().Subject;
        row.Name.Should().Be("Sunset Events");
        row.Products.Should().Be(3);
        row.Tickets.Should().Be(1);
        row.Revenue.Should().Be(80m);
        row.AveragePrice.Should().Be(80m);
        // The operational half stays null rather than claiming a real zero.
        row.Published.Should().BeNull();
        row.Pending.Should().BeNull();
    }

    [Fact]
    public async Task GetOrganizationsAsync_ForAdmin_ReturnsTheOperationalColumnSet()
    {
        _fixture.CatalogClient
            .Setup(c => c.GetOrganizationProductStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CatalogOrganizationProductStats(_orgA, Total: 3, Published: 2, Draft: 1, WithoutImage: 1)]);
        SeedTicket(_sectorA, _productA, 80m, new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc));

        var result = await _sut.GetOrganizationsAsync(Range(), Caller("Admin"));

        result.Value!.View.Should().Be(OrganizationReportView.Operational);

        var row = result.Value.Rows.Should().ContainSingle().Subject;
        row.Published.Should().Be(2);
        row.Pending.Should().Be(1);
        row.WithoutImage.Should().Be(1);
        row.IsPending.Should().BeTrue("an organization with unpublished drafts has work waiting on it");
        // No money columns reach an Admin at all.
        row.Revenue.Should().BeNull();
        row.Tickets.Should().BeNull();
    }

    [Fact]
    public async Task GetOrganizationsAsync_IncludesAnOrganizationThatSoldNothingInTheRange()
    {
        _fixture.CatalogClient
            .Setup(c => c.GetOrganizationProductStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CatalogOrganizationProductStats(_orgB, Total: 1, Published: 1, Draft: 0, WithoutImage: 0)]);

        var result = await _sut.GetOrganizationsAsync(Range(), Caller("SuperAdmin"));

        // A real zero is the point of the tab — a silently missing row would read as "no data".
        var row = result.Value!.Rows.Should().ContainSingle().Subject;
        row.Tickets.Should().Be(0);
        row.Revenue.Should().Be(0m);
        row.GrowthPercent.Should().BeNull();
    }

    // ── Nadolazeći događaji ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUpcomingEventsAsync_ForOrganizer_ScopesCatalogToTheCallersOrganization()
    {
        _fixture.CatalogClient
            .Setup(c => c.GetUpcomingProductsAsync(It.Is<Guid?>(id => id == _orgA), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.GetUpcomingEventsAsync(4, Caller("OrganizationSuperAdmin", _orgA));

        result.IsSuccess.Should().BeTrue();
        _fixture.CatalogClient.Verify(
            c => c.GetUpcomingProductsAsync(_orgA, 4, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUpcomingEventsAsync_ForPlatformStaff_AsksCatalogPlatformWide()
    {
        _fixture.CatalogClient
            .Setup(c => c.GetUpcomingProductsAsync(null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.GetUpcomingEventsAsync(4, Caller("SuperAdmin"));

        result.IsSuccess.Should().BeTrue();
        _fixture.CatalogClient.Verify(
            c => c.GetUpcomingProductsAsync(null, 4, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUpcomingEventsAsync_IsForbiddenToNoOne_TheProductsTabAccessMatrixAdmitsEveryStaffRole()
    {
        // Unlike Sales/Redemption, every staff role reaches Products — Nadolazeći događaji reuses
        // exactly that matrix, so OrganizationAdmin (the narrowest role) must not be refused here.
        _fixture.CatalogClient
            .Setup(c => c.GetUpcomingProductsAsync(It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.GetUpcomingEventsAsync(4, Caller("OrganizationAdmin", _orgA));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetUpcomingEventsAsync_ReturnsCurrentSoldAndCapacityRegardlessOfWhenTicketsWereBought()
    {
        _fixture.CatalogClient
            .Setup(c => c.GetUpcomingProductsAsync(_orgA, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CatalogProductResponse(
                _productA, _orgA, PublishStatus.Published, TicketingMode.SingleOccurrence,
                "Ljetni Festival", new DateTime(2026, 9, 10, 20, 0, 0, DateTimeKind.Utc), City.Mostar)]);

        // Bought well before the reporting-style "recent" window a Sales/Products report would use
        // — GetUpcomingEventsAsync has no date range at all, it wants the event's real current state.
        SeedTicket(_sectorA, _productA, 50m, new DateTime(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc));
        SeedTicket(_sectorA, _productA, 50m, new DateTime(2026, 1, 6, 12, 0, 0, DateTimeKind.Utc));

        var result = await _sut.GetUpcomingEventsAsync(4, Caller("OrganizationSuperAdmin", _orgA));

        var row = result.Value!.Should().ContainSingle().Subject;
        row.ProductId.Should().Be(_productA);
        row.Sold.Should().Be(2);
        row.Capacity.Should().Be(100); // SeedSector(_orgA, _productA, capacity: 100, ...) from the constructor
    }

    [Fact]
    public async Task GetUpcomingEventsAsync_ForOrganizer_LabelsMetaWithSectorCountRatherThanOrganizationName()
    {
        _fixture.CatalogClient
            .Setup(c => c.GetUpcomingProductsAsync(_orgA, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CatalogProductResponse(
                _productA, _orgA, PublishStatus.Published, TicketingMode.SingleOccurrence,
                "Ljetni Festival", new DateTime(2026, 9, 10, 20, 0, 0, DateTimeKind.Utc), City.Mostar)]);

        var result = await _sut.GetUpcomingEventsAsync(4, Caller("OrganizationSuperAdmin", _orgA));

        result.Value!.Should().ContainSingle().Which.Meta.Should().Be("1 sektor · Mostar");
    }

    [Fact]
    public async Task GetUpcomingEventsAsync_ForPlatformStaff_LabelsMetaWithTheOrganizationName()
    {
        _fixture.CatalogClient
            .Setup(c => c.GetUpcomingProductsAsync(null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CatalogProductResponse(
                _productA, _orgA, PublishStatus.Published, TicketingMode.SingleOccurrence,
                "Ljetni Festival", new DateTime(2026, 9, 10, 20, 0, 0, DateTimeKind.Utc), City.Mostar)]);
        _fixture.IdentityClient
            .Setup(c => c.GetOrganizationsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new IdentityOrganizationResponse(_orgA, "Sunset Events", "Mostar", IsActive: true)]);

        var result = await _sut.GetUpcomingEventsAsync(4, Caller("SuperAdmin"));

        result.Value!.Should().ContainSingle().Which.Meta.Should().Be("Sunset Events · Mostar");
    }

    [Fact]
    public async Task GetUpcomingEventsAsync_WhenCatalogHasNothingUpcoming_ReturnsAnEmptyListRatherThanFailing()
    {
        _fixture.CatalogClient
            .Setup(c => c.GetUpcomingProductsAsync(It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.GetUpcomingEventsAsync(4, Caller("SuperAdmin"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }

    // PR #26 review fix: `count` arrives as a bare query-string int on the Gateway-reachable
    // GET /reports/upcoming-events, with no request-DTO validator in front of it (unlike every
    // sibling report endpoint). A negative/huge value used to reach ICatalogClient unchecked and
    // eventually a rejected .Take(count) two services downstream — an unhandled 500 for bad input.
    // Verifying the catalog client is never called proves the bad value is stopped here, not just
    // that the final Result happens to look like a failure.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetUpcomingEventsAsync_WithZeroOrNegativeCount_ReturnsValidationFailureWithoutCallingCatalog(int count)
    {
        var result = await _sut.GetUpcomingEventsAsync(count, Caller("SuperAdmin"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("report.invalid_count");
        _fixture.CatalogClient.Verify(
            c => c.GetUpcomingProductsAsync(It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetUpcomingEventsAsync_WithCountAboveMax_ReturnsValidationFailureWithoutCallingCatalog()
    {
        var result = await _sut.GetUpcomingEventsAsync(51, Caller("SuperAdmin"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("report.invalid_count");
        _fixture.CatalogClient.Verify(
            c => c.GetUpcomingProductsAsync(It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    public void Dispose() => _fixture.Dispose();
}
