using System.Security.Claims;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Security;
using eTicketing.Ticketing.Business.Time;
using eTicketing.Ticketing.Data.Repositories;

namespace eTicketing.Ticketing.Business.Reports;

/// <inheritdoc cref="IReportService"/>
public class ReportService : IReportService
{
    private readonly ITicketRepository _tickets;
    private readonly ISectorRepository _sectors;
    private readonly ICatalogClient _catalog;
    private readonly IIdentityClient _identity;
    private readonly PlatformClock _clock;

    public ReportService(
        ITicketRepository tickets,
        ISectorRepository sectors,
        ICatalogClient catalog,
        IIdentityClient identity,
        PlatformClock clock)
    {
        _tickets = tickets;
        _sectors = sectors;
        _catalog = catalog;
        _identity = identity;
        _clock = clock;
    }

    // ── Public API ───────────────────────────────────────────────────────────────────────────

    public async Task<Result<SalesReportResponse>> GetSalesAsync(
        ReportQuery query, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var access = Authorize(user, ReportTab.Sales);
        if (access.Error is not null)
            return Result<SalesReportResponse>.Failure(access.Error);

        var range = ReportRange.Create(query, _clock);
        var daily = await _tickets.GetDailySalesAsync(access.OrganizationId, range.FromUtc, range.ToUtcExclusive, ct);

        // The comparison period is the same length immediately before this one, so "12,4% više"
        // always means "than the equivalent stretch of time", not "than last calendar month".
        var previous = range.Preceding();
        var previousDaily = await _tickets.GetDailySalesAsync(
            access.OrganizationId, previous.FromUtc, previous.ToUtcExclusive, ct);

        var gross = daily.Sum(d => d.Revenue);
        var sold = daily.Sum(d => d.Sold);
        var cancelledCount = daily.Sum(d => d.Cancelled);
        var cancelledAmount = daily.Sum(d => d.CancelledAmount);
        var previousGross = previousDaily.Sum(d => d.Revenue);

        var scope = await ResolveScopeLabelAsync(access.OrganizationId, ct);

        return new SalesReportResponse(
            Period: range.ToPeriod(),
            Scope: scope,
            GrossRevenue: gross,
            TicketsSold: sold,
            AverageTicketPrice: Divide(gross, sold),
            RevenueChangePercent: previousGross == 0 ? null : Round2((gross - previousGross) / previousGross * 100m),
            CancelledCount: cancelledCount,
            CancelledAmount: cancelledAmount,
            // Share of *money*, not of ticket count: two cancelled VIP seats matter more than two
            // cancelled standing tickets, and the figure sits next to a money total.
            CancellationRatePercent: Round2(Divide(cancelledAmount, gross + cancelledAmount) * 100m),
            NetRevenue: gross - cancelledAmount,
            Buckets: BuildBuckets(range, daily));
    }

    public async Task<Result<ProductReportResponse>> GetProductsAsync(
        ReportQuery query, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var access = Authorize(user, ReportTab.Products);
        if (access.Error is not null)
            return Result<ProductReportResponse>.Failure(access.Error);

        var range = ReportRange.Create(query, _clock);
        var facts = await _tickets.GetProductSalesAsync(access.OrganizationId, range.FromUtc, range.ToUtcExclusive, ct);

        var productIds = facts.Select(f => f.ProductId).ToList();
        var products = (await _catalog.GetProductsAsync(productIds, ct)).ToDictionary(p => p.Id);
        var capacities = (await _sectors.GetPublishedCapacityByProductAsync(productIds, ct))
            .ToDictionary(c => c.ProductId);

        // Platform staff see products from every organization at once, so the second line has to
        // say whose product it is. An organizer already knows — they see only their own — so
        // theirs describes the product's shape instead.
        var organizations = access.OrganizationId is null
            ? await ResolveOrganizationsAsync(products.Values.Select(p => p.OrganizationId), ct)
            : new Dictionary<Guid, IdentityOrganizationResponse>();

        var rows = new List<ProductReportRow>(facts.Count);
        foreach (var fact in facts)
        {
            products.TryGetValue(fact.ProductId, out var product);
            capacities.TryGetValue(fact.ProductId, out var capacity);

            var meta = access.OrganizationId is null
                ? (product is not null && organizations.TryGetValue(product.OrganizationId, out var owner)
                    ? owner.Name
                    : "Nepoznata organizacija")
                : $"{capacity?.SectorCount ?? 0} {SectorWord(capacity?.SectorCount ?? 0)}";

            rows.Add(new ProductReportRow(
                ProductId: fact.ProductId,
                // A ticket outlives the product row it was sold for only if that product was
                // deleted; the sale still happened and still belongs in the totals, so it is
                // labelled rather than dropped.
                Name: product?.Name ?? "Obrisani proizvod",
                Meta: meta,
                Sold: fact.Sold,
                OccupancyPercent: Occupancy(product?.TicketingMode, fact.Sold, capacity?.Capacity),
                AveragePrice: Divide(fact.Revenue, fact.Sold),
                Cancelled: fact.Cancelled,
                Revenue: fact.Revenue));
        }

        rows = [.. rows.OrderByDescending(r => r.Revenue)];

        var withOccupancy = rows.Where(r => r.OccupancyPercent is not null).ToList();
        var totalSold = rows.Sum(r => r.Sold);
        var totalRevenue = rows.Sum(r => r.Revenue);

        return new ProductReportResponse(
            Period: range.ToPeriod(),
            Scope: await ResolveScopeLabelAsync(access.OrganizationId, ct),
            Rows: rows,
            TotalSold: totalSold,
            AverageOccupancyPercent: withOccupancy.Count == 0
                ? null
                : Round2(withOccupancy.Average(r => r.OccupancyPercent!.Value)),
            AveragePrice: Divide(totalRevenue, totalSold),
            TotalCancelled: rows.Sum(r => r.Cancelled),
            TotalRevenue: totalRevenue);
    }

    public async Task<Result<RedemptionReportResponse>> GetRedemptionAsync(
        ReportQuery query, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var access = Authorize(user, ReportTab.Redemption);
        if (access.Error is not null)
            return Result<RedemptionReportResponse>.Failure(access.Error);

        var range = ReportRange.Create(query, _clock);
        var facts = await _tickets.GetRedemptionByProductAsync(access.OrganizationId, range.FromUtc, range.ToUtcExclusive, ct);
        var byHour = await _tickets.GetCheckinsByHourAsync(access.OrganizationId, range.FromUtc, range.ToUtcExclusive, ct);

        var products = (await _catalog.GetProductsAsync(facts.Select(f => f.ProductId).ToList(), ct))
            .ToDictionary(p => p.Id);

        var rows = facts
            .Select(f => new RedemptionReportRow(
                ProductId: f.ProductId,
                Name: products.TryGetValue(f.ProductId, out var p) ? p.Name : "Obrisani proizvod",
                Sold: f.Sold,
                CheckedIn: f.CheckedIn,
                NoShow: Math.Max(0, f.Sold - f.CheckedIn),
                Printed: f.Printed,
                RatePercent: Round2(Divide(f.CheckedIn, f.Sold) * 100m)))
            .OrderByDescending(r => r.Sold)
            .ToList();

        var totalSold = facts.Sum(f => f.Sold);
        var totalCheckedIn = facts.Sum(f => f.CheckedIn);
        var totalScans = byHour.Sum(h => h.Count);
        var peak = byHour.Count == 0 ? null : byHour.MaxBy(h => h.Count);

        return new RedemptionReportResponse(
            Period: range.ToPeriod(),
            Scope: await ResolveScopeLabelAsync(access.OrganizationId, ct),
            TotalCheckedIn: totalCheckedIn,
            NoShowRatePercent: Round2(Divide(totalSold - totalCheckedIn, totalSold) * 100m),
            PeakHour: peak?.Hour,
            PeakHourSharePercent: peak is null ? null : Round2(Divide(peak.Count, totalScans) * 100m),
            PrintedTickets: facts.Sum(f => f.Printed),
            CheckinsByHour: [.. byHour.Select(h => new CheckinHourPoint(h.Hour, h.Count))],
            Rows: rows);
    }

    public async Task<Result<OrganizationReportResponse>> GetOrganizationsAsync(
        ReportQuery query, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var access = Authorize(user, ReportTab.Organizations);
        if (access.Error is not null)
            return Result<OrganizationReportResponse>.Failure(access.Error);

        var range = ReportRange.Create(query, _clock);

        // Catalog is the authority on which organizations exist as far as this report cares: an
        // organization with no products has nothing to report on, and one with products but no
        // sales still belongs in the table (showing a real zero, which is the point of the tab).
        var productStats = await _catalog.GetOrganizationProductStatsAsync(ct);
        var financial = user.IsInRole("SuperAdmin");

        // Admin's operational view has no money columns at all, so the two sales queries are
        // skipped outright for that role rather than fetched and thrown away.
        var sales = new Dictionary<Guid, OrganizationSalesFacts>();
        var previousSales = new Dictionary<Guid, OrganizationSalesFacts>();
        if (financial)
        {
            var previousRange = range.Preceding();
            sales = (await _tickets.GetOrganizationSalesAsync(range.FromUtc, range.ToUtcExclusive, ct))
                .ToDictionary(s => s.OrganizationId);
            previousSales = (await _tickets.GetOrganizationSalesAsync(previousRange.FromUtc, previousRange.ToUtcExclusive, ct))
                .ToDictionary(s => s.OrganizationId);
        }

        var organizations = await ResolveOrganizationsAsync(productStats.Select(s => s.OrganizationId), ct);

        var rows = new List<OrganizationReportRow>(productStats.Count);
        foreach (var stats in productStats)
        {
            organizations.TryGetValue(stats.OrganizationId, out var org);
            sales.TryGetValue(stats.OrganizationId, out var sale);
            previousSales.TryGetValue(stats.OrganizationId, out var previousSale);

            var revenue = sale?.Revenue ?? 0m;
            var previousRevenue = previousSale?.Revenue ?? 0m;

            rows.Add(new OrganizationReportRow(
                OrganizationId: stats.OrganizationId,
                Name: org?.Name ?? "Nepoznata organizacija",
                Address: org?.Address ?? string.Empty,
                Products: stats.Total,
                Tickets: financial ? sale?.Sold ?? 0 : null,
                AveragePrice: financial ? Divide(revenue, sale?.Sold ?? 0) : null,
                GrowthPercent: financial && previousRevenue != 0
                    ? Round2((revenue - previousRevenue) / previousRevenue * 100m)
                    : null,
                Revenue: financial ? revenue : null,
                Published: financial ? null : stats.Published,
                Pending: financial ? null : stats.Draft,
                WithoutImage: financial ? null : stats.WithoutImage,
                // "Na čekanju" is a derived state, not a stored one: an organization with unpublished
                // drafts has work waiting on it, which is exactly what an Admin's operational view
                // is scanning the column for.
                IsPending: financial ? null : stats.Draft > 0));
        }

        return new OrganizationReportResponse(
            Period: range.ToPeriod(),
            View: financial ? OrganizationReportView.Financial : OrganizationReportView.Operational,
            Rows: financial
                ? [.. rows.OrderByDescending(r => r.Revenue)]
                : [.. rows.OrderByDescending(r => r.Pending).ThenBy(r => r.Name)]);
    }

    // ── Authorization ────────────────────────────────────────────────────────────────────────

    /// <summary>The outcome of the role/tab matrix check: either an <see cref="Error"/> to return,
    /// or the organization the caller's data must be scoped to (null = whole platform).</summary>
    private readonly record struct ReportAccess(Error? Error, Guid? OrganizationId)
    {
        public static ReportAccess Denied(Error error) => new(error, null);
        public static ReportAccess Platform() => new(null, null);
        public static ReportAccess Organization(Guid id) => new(null, id);
    }

    /// <summary>
    /// The single place the report matrix lives. Written as an explicit switch over role × tab
    /// rather than as a set of authorization policies because the split is not "can you reach this
    /// endpoint" but "which of the four reports does your job involve" — Admin and SuperAdmin
    /// share every ASP.NET policy in this codebase yet see different tabs here.
    /// </summary>
    private static ReportAccess Authorize(ClaimsPrincipal user, ReportTab tab)
    {
        var forbidden = Error.Unauthorized("report.forbidden", "Nemate pristup ovom izvještaju.");

        switch (user.GetRole())
        {
            case "SuperAdmin":
                return ReportAccess.Platform();

            case "Admin":
                // Platform-wide reach, operational remit: no revenue reports, no gate statistics.
                return tab is ReportTab.Products or ReportTab.Organizations
                    ? ReportAccess.Platform()
                    : ReportAccess.Denied(forbidden);

            case "OrganizationSuperAdmin":
            case "OrganizationAdmin":
            {
                if (tab == ReportTab.Organizations)
                    return ReportAccess.Denied(forbidden);

                // OrganizationAdmin is the narrowest role: sales and products for their own
                // organization only, no gate statistics and no export (see IReportPdfService).
                if (tab == ReportTab.Redemption && user.IsInRole("OrganizationAdmin"))
                    return ReportAccess.Denied(forbidden);

                var organizationId = user.GetOrganizationId();
                if (organizationId is null)
                {
                    // An Org* token with no organizationId claim is a malformed session, not a
                    // permission question — refuse rather than silently widening to the platform.
                    return ReportAccess.Denied(Error.Unauthorized(
                        "report.no_organization", "Vaš nalog nije povezan ni sa jednom organizacijom."));
                }

                return ReportAccess.Organization(organizationId.Value);
            }

            default:
                return ReportAccess.Denied(forbidden);
        }
    }

    // ── Range + bucketing ────────────────────────────────────────────────────────────────────

    /// <summary>A validated report range, resolved once into every form the pipeline needs: the
    /// local dates for labels, the half-open UTC window for SQL, and the bucket unit the chart is
    /// drawn in.</summary>
    private readonly record struct ReportRange(
        DateOnly From, DateOnly To, DateTime FromUtc, DateTime ToUtcExclusive, PlatformClock Clock)
    {
        public int Days => To.DayNumber - From.DayNumber + 1;

        public static ReportRange Create(ReportQuery query, PlatformClock clock) => new(
            query.From,
            query.To,
            clock.ToUtcStartOfDay(query.From),
            // Exclusive upper bound at the start of the day *after* To, so the whole of the last
            // day is included without any 23:59:59.999 rounding games.
            clock.ToUtcStartOfDay(query.To.AddDays(1)),
            clock);

        /// <summary>The equal-length range immediately before this one — the growth comparison
        /// baseline.</summary>
        public ReportRange Preceding() => Create(
            new ReportQuery { From = From.AddDays(-Days), To = From.AddDays(-1) }, Clock);

        /// <summary>Daily bars up to a fortnight, weekly up to a quarter, monthly beyond. Chosen
        /// here rather than by the client so the PDF and all three frontends can never disagree
        /// about what a bar means.</summary>
        public ReportBucketUnit Unit => Days switch
        {
            <= 14 => ReportBucketUnit.Day,
            <= 92 => ReportBucketUnit.Week,
            _ => ReportBucketUnit.Month
        };

        public ReportPeriod ToPeriod() => new(From, To, Days, Unit);
    }

    /// <summary>Bosnian short month names, matching the desktop app's own formatting and the
    /// labels in docs/Design/Reports.dc.html.</summary>
    private static readonly string[] MonthNames =
        ["jan", "feb", "mar", "apr", "maj", "jun", "jul", "avg", "sep", "okt", "nov", "dec"];

    /// <summary>
    /// Folds the per-day rows into the chart's bars. Buckets are generated from the range itself
    /// rather than from the rows, so a week nobody bought anything in is drawn as a zero-height bar
    /// instead of vanishing and silently compressing the timeline.
    /// </summary>
    private static List<ReportBucket> BuildBuckets(ReportRange range, List<DailySalesFacts> daily)
    {
        var byDay = daily.ToDictionary(d => d.Day);
        var buckets = new List<ReportBucket>();

        // Week and month buckets are anchored at the start of the range and walk forward, so the
        // first bar is always the range's own start date rather than a partial period carved
        // backwards from today.
        var cursor = range.From;
        while (cursor <= range.To)
        {
            var end = range.Unit switch
            {
                ReportBucketUnit.Day => cursor,
                ReportBucketUnit.Week => cursor.AddDays(6),
                _ => new DateOnly(cursor.Year, cursor.Month, 1).AddMonths(1).AddDays(-1)
            };
            if (end > range.To) end = range.To;

            decimal revenue = 0;
            var sold = 0;
            for (var day = cursor; day <= end; day = day.AddDays(1))
            {
                if (!byDay.TryGetValue(day, out var facts)) continue;
                revenue += facts.Revenue;
                sold += facts.Sold;
            }

            buckets.Add(new ReportBucket(Label(cursor, range.Unit), revenue, sold));
            cursor = end.AddDays(1);
        }

        return buckets;
    }

    private static string Label(DateOnly start, ReportBucketUnit unit) => unit switch
    {
        ReportBucketUnit.Month => char.ToUpperInvariant(MonthNames[start.Month - 1][0]) + MonthNames[start.Month - 1][1..],
        _ => $"{start.Day}. {MonthNames[start.Month - 1]}"
    };

    // ── Cross-service lookups ────────────────────────────────────────────────────────────────

    /// <summary>The report's subtitle and PDF header line: the platform as a whole, or the name of
    /// the one organization the caller may see.</summary>
    private async Task<string> ResolveScopeLabelAsync(Guid? organizationId, CancellationToken ct)
    {
        if (organizationId is null)
            return "Platforma";

        var organizations = await _identity.GetOrganizationsAsync([organizationId.Value], ct);
        return organizations.FirstOrDefault()?.Name ?? "Vaša organizacija";
    }

    /// <summary>Resolves the organizations a report has rows for, keyed by id. Unknown ids are
    /// absent rather than an error — see IIdentityClient.</summary>
    private async Task<Dictionary<Guid, IdentityOrganizationResponse>> ResolveOrganizationsAsync(
        IEnumerable<Guid> organizationIds, CancellationToken ct)
    {
        var ids = organizationIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, IdentityOrganizationResponse>();

        var organizations = await _identity.GetOrganizationsAsync(ids, ct);
        return organizations.ToDictionary(o => o.Id);
    }

    // ── Arithmetic helpers ───────────────────────────────────────────────────────────────────

    /// <summary>Division that answers 0 instead of throwing on an empty period. Every ratio in
    /// these reports has a legitimately-zero denominator (a range with no sales, a product with no
    /// check-ins), so guarding at each call site would be noise.</summary>
    private static decimal Divide(decimal numerator, decimal denominator)
        => denominator == 0 ? 0m : numerator / denominator;

    /// <summary>Percentages and averages are rounded once, here, so the clients can render what
    /// they are given rather than each rounding a long decimal their own way.</summary>
    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Occupancy needs a fixed total to divide by. DailyEntry capacity is a per-day
    /// allowance repeated across a month, so there is no such total — those products report no
    /// occupancy at all rather than a number derived from the wrong denominator.</summary>
    private static decimal? Occupancy(TicketingMode? mode, int sold, int? capacity)
    {
        if (mode is null or TicketingMode.DailyEntry || capacity is null or 0)
            return null;

        // Capped at 100: a sector resized downwards after tickets were sold would otherwise
        // produce an occupancy above full, which reads as a bug rather than as history.
        return Round2(Math.Min(100m, (decimal)sold / capacity.Value * 100m));
    }

    private static string SectorWord(int count) => count == 1 ? "sektor" : "sektora";
}
