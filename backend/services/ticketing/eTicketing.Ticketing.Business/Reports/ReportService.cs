using System.Security.Claims;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Ticketing.Business.External;
using eTicketing.Contracts.Security;
using eTicketing.Ticketing.Business.Security;
using eTicketing.Ticketing.Business.Time;
using eTicketing.Ticketing.Data.Entities;
using eTicketing.Ticketing.Data.Repositories;
using static eTicketing.Ticketing.Business.Reports.ReportMath;

namespace eTicketing.Ticketing.Business.Reports;

/// <inheritdoc cref="IReportService"/>
public class ReportService : IReportService
{
    private readonly ITicketRepository _tickets;
    private readonly ISectorRepository _sectors;
    private readonly ICatalogClient _catalog;
    private readonly IOrganizationSnapshotRepository _organizations;
    private readonly PlatformClock _clock;

    public ReportService(
        ITicketRepository tickets,
        ISectorRepository sectors,
        ICatalogClient catalog,
        IOrganizationSnapshotRepository organizations,
        PlatformClock clock)
    {
        _tickets = tickets;
        _sectors = sectors;
        _catalog = catalog;
        _organizations = organizations;
        _clock = clock;
    }

    // ── Public API ───────────────────────────────────────────────────────────────────────────

    public async Task<Result<SalesReportResponse>> GetSalesAsync(
        ReportQuery query, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var access = ReportAuthorization.Authorize(user, ReportTab.Sales);
        if (access.Error is not null)
            return Result<SalesReportResponse>.Failure(access.Error);

        var range = ReportRange.Create(query, _clock);
        var daily = ReportSeries.ToLocalDays(
            await _tickets.GetHourlySalesAsync(access.OrganizationId, range.FromUtc, range.ToUtcExclusive, ct), _clock);

        // The comparison period is the same length immediately before this one, so "12,4% više"
        // always means "than the equivalent stretch of time", not "than last calendar month".
        var previous = range.Preceding();
        var previousDaily = ReportSeries.ToLocalDays(
            await _tickets.GetHourlySalesAsync(access.OrganizationId, previous.FromUtc, previous.ToUtcExclusive, ct), _clock);

        // Every total below is summed over the SAME local-day list the chart is built from, so the
        // headline figures and the bars can never disagree. They could before: the rows were keyed
        // by UTC day while BuildBuckets walked local days, so a sale in the first hour or two of
        // the range landed on a day the cursor never visited — counted in the total, invisible in
        // every bar.
        var gross = daily.Values.Sum(d => d.Revenue);
        var sold = daily.Values.Sum(d => d.Sold);
        var cancelledCount = daily.Values.Sum(d => d.Cancelled);
        var cancelledAmount = daily.Values.Sum(d => d.CancelledAmount);
        var onlineSold = daily.Values.Sum(d => d.OnlineSold);
        var printedSold = daily.Values.Sum(d => d.PrintedSold);
        var previousGross = previousDaily.Values.Sum(d => d.Revenue);

        var scope = await ResolveScopeLabelAsync(access.OrganizationId, ct);
        var byOrganization = await BuildSalesByOrganizationAsync(access.OrganizationId, range, gross, ct);

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
            OnlineSold: onlineSold,
            PrintedSold: printedSold,
            Buckets: ReportSeries.BuildBuckets(range, daily),
            ByOrganization: byOrganization);
    }

    /// <summary>
    /// Splits the Prodaja tab's headline revenue by owning organization, for a platform-wide
    /// caller only. An organizer's report is already one organization's, so the breakdown would
    /// be a single row repeating the totals above it — they get an empty list and the UI drops
    /// the section.
    ///
    /// <para><paramref name="gross"/> is passed in rather than recomputed so the shares are a
    /// share of the very number the tiles and the chart display. Recomputing it here from the
    /// same repository would be one more place for the two to drift.</para>
    /// </summary>
    private async Task<IReadOnlyList<SalesByOrganizationRow>> BuildSalesByOrganizationAsync(
        Guid? organizationId, ReportRange range, decimal gross, CancellationToken ct)
    {
        if (organizationId is not null)
            return [];

        var facts = await _tickets.GetOrganizationSalesAsync(range.FromUtc, range.ToUtcExclusive, ct);
        if (facts.Count == 0)
            return [];

        var organizations = await ResolveOrganizationsAsync(facts.Select(f => f.OrganizationId), ct);

        return
        [
            .. facts
                .Select(fact =>
                {
                    organizations.TryGetValue(fact.OrganizationId, out var organization);
                    return new SalesByOrganizationRow(
                        OrganizationId: fact.OrganizationId,
                        // Same fallback as the Organizacije tab: an organization Identity no
                        // longer knows about still owns real sales, and dropping the row would
                        // silently unbalance the totals.
                        Name: organization?.Name ?? "Nepoznata organizacija",
                        Sold: fact.Sold,
                        Revenue: fact.Revenue,
                        AveragePrice: Divide(fact.Revenue, fact.Sold),
                        SharePercent: Round2(Divide(fact.Revenue, gross) * 100m));
                })
                .OrderByDescending(row => row.Revenue)
        ];
    }

    public async Task<Result<ProductReportResponse>> GetProductsAsync(
        ReportQuery query, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var access = ReportAuthorization.Authorize(user, ReportTab.Products);
        if (access.Error is not null)
            return Result<ProductReportResponse>.Failure(access.Error);

        var range = ReportRange.Create(query, _clock);
        var facts = await _tickets.GetProductSalesAsync(access.OrganizationId, range.FromUtc, range.ToUtcExclusive, ct);

        var productIds = facts.Select(f => f.ProductId).ToList();
        var products = (await FetchProductsAsync(productIds, ct)).ToDictionary(p => p.Id);
        var capacities = (await _sectors.GetPublishedCapacityByProductAsync(productIds, ct))
            .ToDictionary(c => c.ProductId);

        // Platform staff see products from every organization at once, so the second line has to
        // say whose product it is. An organizer already knows — they see only their own — so
        // theirs describes the product's shape instead.
        var organizations = access.OrganizationId is null
            ? await ResolveOrganizationsAsync(products.Values.Select(p => p.OrganizationId), ct)
            : new Dictionary<Guid, OrganizationSnapshot>();

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
        var access = ReportAuthorization.Authorize(user, ReportTab.Redemption);
        if (access.Error is not null)
            return Result<RedemptionReportResponse>.Failure(access.Error);

        var range = ReportRange.Create(query, _clock);
        var facts = await _tickets.GetRedemptionByProductAsync(access.OrganizationId, range.FromUtc, range.ToUtcExclusive, ct);
        var scannedAtUtc = await _tickets.GetCheckinTimestampsAsync(
            access.OrganizationId, range.FromUtc, range.ToUtcExclusive, ct);

        // Converted here, not in the repository: the hour a report shows is the hour the gate
        // staff experienced, and only this layer knows the platform's time zone. Grouping the raw
        // UTC instants would put a 21:00 CEST arrival in the 19h column, every night.
        var byHour = scannedAtUtc
            .GroupBy(at => _clock.ToLocal(at).Hour)
            .Select(g => new CheckinHourPoint(g.Key, g.Count()))
            .OrderBy(h => h.Hour)
            .ToList();

        var products = (await FetchProductsAsync([.. facts.Select(f => f.ProductId)], ct))
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
        var totalScans = scannedAtUtc.Count;
        var peak = byHour.Count == 0 ? null : byHour.MaxBy(h => h.Count);

        return new RedemptionReportResponse(
            Period: range.ToPeriod(),
            Scope: await ResolveScopeLabelAsync(access.OrganizationId, ct),
            TotalCheckedIn: totalCheckedIn,
            NoShowRatePercent: Round2(Divide(totalSold - totalCheckedIn, totalSold) * 100m),
            PeakHour: peak?.Hour,
            PeakHourSharePercent: peak is null ? null : Round2(Divide(peak.Count, totalScans) * 100m),
            PrintedTickets: facts.Sum(f => f.Printed),
            CheckinsByHour: byHour,
            Rows: rows);
    }

    public async Task<Result<OrganizationReportResponse>> GetOrganizationsAsync(
        ReportQuery query, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var access = ReportAuthorization.Authorize(user, ReportTab.Organizations);
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

    public async Task<Result<List<UpcomingEventResponse>>> GetUpcomingEventsAsync(
        int count, ClaimsPrincipal user, CancellationToken ct = default)
    {
        // Reuses the Products tab's access matrix rather than a bespoke check: every staff role may
        // see it (unlike Sales/Redemption, which Admin/OrganizationAdmin are denied), scoped to the
        // caller's own organization for Org* roles and platform-wide for SuperAdmin/Admin — exactly
        // the shape "which events are coming up" needs for the Dashboard.
        var access = ReportAuthorization.Authorize(user, ReportTab.Products);
        if (access.Error is not null)
            return Result<List<UpcomingEventResponse>>.Failure(access.Error);

        if (count <= 0 || count > MaxUpcomingCount)
        {
            return Result<List<UpcomingEventResponse>>.Failure(Error.Validation(
                "report.invalid_count", $"Broj događaja mora biti između 1 i {MaxUpcomingCount}."));
        }

        var products = await _catalog.GetUpcomingProductsAsync(access.OrganizationId, count, ct);
        if (products.Count == 0)
            return Result<List<UpcomingEventResponse>>.Success([]);

        var productIds = products.Select(p => p.Id).ToList();
        var capacities = (await _sectors.GetPublishedCapacityByProductAsync(productIds, ct)).ToDictionary(c => c.ProductId);
        var sold = (await _tickets.GetSoldCountsByProductIdsAsync(productIds, ct)).ToDictionary(s => s.ProductId);

        // Platform staff need to know whose event this is; an organizer already knows (they only
        // see their own), so their line describes the product's own shape instead — the same split
        // GetProductsAsync's Meta column uses.
        var organizations = access.OrganizationId is null
            ? await ResolveOrganizationsAsync(products.Select(p => p.OrganizationId), ct)
            : new Dictionary<Guid, OrganizationSnapshot>();

        var result = products.Select(p =>
        {
            capacities.TryGetValue(p.Id, out var capacity);
            sold.TryGetValue(p.Id, out var soldCount);

            var meta = access.OrganizationId is null
                ? (organizations.TryGetValue(p.OrganizationId, out var owner)
                    ? $"{owner.Name} · {p.City.ToDisplayName()}"
                    : p.City.ToDisplayName())
                : $"{capacity?.SectorCount ?? 0} {SectorWord(capacity?.SectorCount ?? 0)} · {p.City.ToDisplayName()}";

            return new UpcomingEventResponse(
                ProductId: p.Id,
                Name: p.Name,
                Meta: meta,
                // Catalog's GetUpcomingAsync already filters to Date != null && Date >= now, so this
                // is always populated for every row it returns.
                Date: p.Date!.Value,
                Sold: soldCount?.Sold ?? 0,
                Capacity: capacity?.Capacity ?? 0);
        }).ToList();

        return Result<List<UpcomingEventResponse>>.Success(result);
    }

    // ── Cross-service lookups ────────────────────────────────────────────────────────────────

    /// <summary>The report's subtitle and PDF header line: the platform as a whole, or the name of
    /// the one organization the caller may see.</summary>
    private async Task<string> ResolveScopeLabelAsync(Guid? organizationId, CancellationToken ct)
    {
        if (organizationId is null)
            return "Platforma";

        var organization = await _organizations.GetByIdNoTrackingAsync(organizationId.Value, ct);
        return organization?.Name ?? "Vaša organizacija";
    }

    /// <summary>Resolves the organizations a report has rows for, keyed by id. Unknown ids are
    /// absent rather than an error, and the callers label those rows "Nepoznata organizacija" —
    /// the identical degradation the old HTTP lookup had when eTicketing.Identity answered short
    /// or not at all. Local now, so there is no batch cap to respect and no round trip to fail.
    /// </summary>
    private async Task<Dictionary<Guid, OrganizationSnapshot>> ResolveOrganizationsAsync(
        IEnumerable<Guid> organizationIds, CancellationToken ct)
    {
        var ids = organizationIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, OrganizationSnapshot>();

        var organizations = await _organizations.GetByIdsNoTrackingAsync(ids, ct);
        return organizations.ToDictionary(o => o.OrganizationId);
    }

    /// <summary>Product lookup, batched. Split out so both the Proizvodi and the Iskorištenost
    /// report hit the same path.</summary>
    private Task<List<CatalogProductResponse>> FetchProductsAsync(
        IReadOnlyList<Guid> productIds, CancellationToken ct)
        => FetchInBatchesAsync(productIds, _catalog.GetProductsAsync, ct);

    /// <summary>
    /// Upper bound on how many ids one internal lookup may carry, mirroring the identical cap in
    /// Catalog's ProductService and Identity's OrganizationService.
    ///
    /// Duplicated rather than shared because the two sides are separate services that only share
    /// an HTTP contract — but it has to be respected here, because those services answer a longer
    /// list with a 400 and both HTTP clients call EnsureSuccessStatusCode, which turns that into an
    /// HttpRequestException and an opaque 500. A platform-wide year-long report can easily name
    /// more than 200 products, and the range validator accepts exactly that request.
    /// </summary>
    private const int MaxIdsPerLookup = 200;

    /// <summary>
    /// Upper bound on <see cref="GetUpcomingEventsAsync"/>'s <c>count</c>. That parameter arrives
    /// as a bare query-string int on a Gateway-reachable endpoint, not through a validated request
    /// DTO, so this is the only thing standing between a bad/malicious value and a negative or
    /// unbounded <c>.Take(count)</c> two services downstream (Catalog's ProductRepository).
    /// Duplicated in Catalog's ProductService rather than shared, same reasoning as
    /// MaxIdsPerLookup above — the two sides only share an HTTP contract, not code — but it must be
    /// enforced here too so a bad value never leaves this service in the first place.
    /// </summary>
    private const int MaxUpcomingCount = 50;

    /// <summary>Runs a batch lookup in chunks of <see cref="MaxIdsPerLookup"/> and concatenates the
    /// results, so a wide report resolves every name instead of failing outright.</summary>
    private static async Task<List<T>> FetchInBatchesAsync<T>(
        IReadOnlyList<Guid> ids,
        Func<IReadOnlyList<Guid>, CancellationToken, Task<IReadOnlyList<T>>> fetch,
        CancellationToken ct)
    {
        if (ids.Count == 0)
            return [];

        var results = new List<T>(ids.Count);
        for (var offset = 0; offset < ids.Count; offset += MaxIdsPerLookup)
        {
            var batch = ids.Skip(offset).Take(MaxIdsPerLookup).ToList();
            results.AddRange(await fetch(batch, ct));
        }

        return results;
    }

    // ── Arithmetic helpers ───────────────────────────────────────────────────────────────────

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
