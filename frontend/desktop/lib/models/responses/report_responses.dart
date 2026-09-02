/// The four report shapes returned by `GET /api/reports/*`.
///
/// They live in one file rather than four because they share `ReportPeriod` and
/// are always consumed together by a single screen — splitting them would mean
/// four imports for every widget that renders a tab.
///
/// Enums are parsed by **ordinal**. The backend registers no
/// `JsonStringEnumConverter` anywhere, so `System.Text.Json` serializes every
/// enum as its integer value — which is why the C# side documents these as
/// "append only, never reorder". Parsing by name here silently fell through to
/// the `orElse` fallback on every response.
library;

/// How the sales chart is bucketed. The server picks this from the range
/// length and tells the client, so the chart title can say which it is.
enum ReportBucketUnit {
  day('po danu'),
  week('po sedmici'),
  month('po mjesecu');

  const ReportBucketUnit(this.title);

  /// The tail of the chart heading — "Prihod po sedmici".
  final String title;

  static ReportBucketUnit fromJson(Object? value) => _byOrdinal(values, value, ReportBucketUnit.day);
}

/// Which column set the Organizacije table renders. SuperAdmin gets the
/// financial view, Admin the operational one.
enum OrganizationReportView {
  financial,
  operational;

  static OrganizationReportView fromJson(Object? value) =>
      _byOrdinal(values, value, OrganizationReportView.operational);
}

/// The range a report actually covers, echoed back by the server.
class ReportPeriod {
  final DateTime from;
  final DateTime to;
  final int days;
  final ReportBucketUnit bucketUnit;

  const ReportPeriod({
    required this.from,
    required this.to,
    required this.days,
    required this.bucketUnit,
  });

  factory ReportPeriod.fromJson(Map<String, dynamic> json) => ReportPeriod(
        from: DateTime.parse(json['from'] as String),
        to: DateTime.parse(json['to'] as String),
        days: json['days'] as int? ?? 0,
        bucketUnit: ReportBucketUnit.fromJson(json['bucketUnit']),
      );
}

/// One bar of the sales chart. The label arrives pre-formatted in Bosnian —
/// the bucketing rules live on the server so all three clients agree.
class ReportBucket {
  final String label;
  final double revenue;
  final int sold;

  const ReportBucket({required this.label, required this.revenue, required this.sold});

  factory ReportBucket.fromJson(Map<String, dynamic> json) => ReportBucket(
        label: json['label'] as String? ?? '',
        revenue: _toDouble(json['revenue']),
        sold: json['sold'] as int? ?? 0,
      );
}

/// One row of the Prodaja tab's per-organization breakdown. Sent only to a platform-wide caller;
/// an organizer receives an empty list, since their whole report is already one organization's.
class SalesByOrganizationRow {
  final String organizationId;
  final String name;
  final int sold;
  final double revenue;
  final double averagePrice;

  /// Share of the report's gross revenue, so the column sums to 100% across the rows.
  final double sharePercent;

  const SalesByOrganizationRow({
    required this.organizationId,
    required this.name,
    required this.sold,
    required this.revenue,
    required this.averagePrice,
    required this.sharePercent,
  });

  factory SalesByOrganizationRow.fromJson(Map<String, dynamic> json) => SalesByOrganizationRow(
        organizationId: json['organizationId'] as String? ?? '',
        name: json['name'] as String? ?? '',
        sold: json['sold'] as int? ?? 0,
        revenue: _toDouble(json['revenue']),
        averagePrice: _toDouble(json['averagePrice']),
        sharePercent: _toDouble(json['sharePercent']),
      );
}

class SalesReport {
  final ReportPeriod period;
  final String scope;
  final double grossRevenue;
  final int ticketsSold;
  final double averageTicketPrice;

  /// Null when the preceding period sold nothing — the UI drops the comparison
  /// line rather than showing an infinite increase.
  final double? revenueChangePercent;

  final int cancelledCount;
  final double cancelledAmount;
  final double cancellationRatePercent;
  final double netRevenue;

  /// Sales-channel split — the two real `TicketOrigin` values. `onlineSold + printedSold ==
  /// ticketsSold`. Not rendered on the Reports screen, only on the Dashboard's "Kanali prodaje" card.
  final int onlineSold;
  final int printedSold;

  final List<ReportBucket> buckets;

  /// Empty for an organizer — the Prodaja tab omits the breakdown section entirely rather than
  /// rendering a one-row table that restates the headline tiles.
  final List<SalesByOrganizationRow> byOrganization;

  const SalesReport({
    required this.period,
    required this.scope,
    required this.grossRevenue,
    required this.ticketsSold,
    required this.averageTicketPrice,
    required this.revenueChangePercent,
    required this.cancelledCount,
    required this.cancelledAmount,
    required this.cancellationRatePercent,
    required this.netRevenue,
    required this.onlineSold,
    required this.printedSold,
    required this.buckets,
    required this.byOrganization,
  });

  factory SalesReport.fromJson(Map<String, dynamic> json) => SalesReport(
        period: ReportPeriod.fromJson(json['period'] as Map<String, dynamic>),
        scope: json['scope'] as String? ?? '',
        grossRevenue: _toDouble(json['grossRevenue']),
        ticketsSold: json['ticketsSold'] as int? ?? 0,
        averageTicketPrice: _toDouble(json['averageTicketPrice']),
        revenueChangePercent: _toNullableDouble(json['revenueChangePercent']),
        cancelledCount: json['cancelledCount'] as int? ?? 0,
        cancelledAmount: _toDouble(json['cancelledAmount']),
        cancellationRatePercent: _toDouble(json['cancellationRatePercent']),
        netRevenue: _toDouble(json['netRevenue']),
        onlineSold: json['onlineSold'] as int? ?? 0,
        printedSold: json['printedSold'] as int? ?? 0,
        buckets: _list(json['buckets'], ReportBucket.fromJson),
        byOrganization: _list(json['byOrganization'], SalesByOrganizationRow.fromJson),
      );
}

class ProductReportRow {
  final String productId;
  final String name;

  /// The small grey second line: the owning organization for platform staff,
  /// the product's sector count for an organizer.
  final String meta;

  final int sold;

  /// Null for DailyEntry products, whose per-day capacity has no single
  /// denominator — rendered as "—".
  final double? occupancyPercent;

  final double averagePrice;
  final int cancelled;
  final double revenue;

  const ProductReportRow({
    required this.productId,
    required this.name,
    required this.meta,
    required this.sold,
    required this.occupancyPercent,
    required this.averagePrice,
    required this.cancelled,
    required this.revenue,
  });

  factory ProductReportRow.fromJson(Map<String, dynamic> json) => ProductReportRow(
        productId: json['productId'] as String? ?? '',
        name: json['name'] as String? ?? '',
        meta: json['meta'] as String? ?? '',
        sold: json['sold'] as int? ?? 0,
        occupancyPercent: _toNullableDouble(json['occupancyPercent']),
        averagePrice: _toDouble(json['averagePrice']),
        cancelled: json['cancelled'] as int? ?? 0,
        revenue: _toDouble(json['revenue']),
      );
}

class ProductReport {
  final ReportPeriod period;
  final String scope;
  final List<ProductReportRow> rows;
  final int totalSold;
  final double? averageOccupancyPercent;
  final double averagePrice;
  final int totalCancelled;
  final double totalRevenue;

  const ProductReport({
    required this.period,
    required this.scope,
    required this.rows,
    required this.totalSold,
    required this.averageOccupancyPercent,
    required this.averagePrice,
    required this.totalCancelled,
    required this.totalRevenue,
  });

  factory ProductReport.fromJson(Map<String, dynamic> json) => ProductReport(
        period: ReportPeriod.fromJson(json['period'] as Map<String, dynamic>),
        scope: json['scope'] as String? ?? '',
        rows: _list(json['rows'], ProductReportRow.fromJson),
        totalSold: json['totalSold'] as int? ?? 0,
        averageOccupancyPercent: _toNullableDouble(json['averageOccupancyPercent']),
        averagePrice: _toDouble(json['averagePrice']),
        totalCancelled: json['totalCancelled'] as int? ?? 0,
        totalRevenue: _toDouble(json['totalRevenue']),
      );
}

/// One bar of the "Dolazak po satu" chart. [hour] is 0-23.
class CheckinHourPoint {
  final int hour;
  final int count;

  const CheckinHourPoint({required this.hour, required this.count});

  factory CheckinHourPoint.fromJson(Map<String, dynamic> json) => CheckinHourPoint(
        hour: json['hour'] as int? ?? 0,
        count: json['count'] as int? ?? 0,
      );
}

class RedemptionReportRow {
  final String productId;
  final String name;
  final int sold;
  final int checkedIn;
  final int noShow;
  final int printed;
  final double ratePercent;

  const RedemptionReportRow({
    required this.productId,
    required this.name,
    required this.sold,
    required this.checkedIn,
    required this.noShow,
    required this.printed,
    required this.ratePercent,
  });

  factory RedemptionReportRow.fromJson(Map<String, dynamic> json) => RedemptionReportRow(
        productId: json['productId'] as String? ?? '',
        name: json['name'] as String? ?? '',
        sold: json['sold'] as int? ?? 0,
        checkedIn: json['checkedIn'] as int? ?? 0,
        noShow: json['noShow'] as int? ?? 0,
        printed: json['printed'] as int? ?? 0,
        ratePercent: _toDouble(json['ratePercent']),
      );
}

class RedemptionReport {
  final ReportPeriod period;
  final String scope;
  final int totalCheckedIn;
  final double noShowRatePercent;

  /// Null when nothing was scanned in the range at all — there is no busiest
  /// hour of an empty histogram.
  final int? peakHour;
  final double? peakHourSharePercent;

  final int printedTickets;
  final List<CheckinHourPoint> checkinsByHour;
  final List<RedemptionReportRow> rows;

  const RedemptionReport({
    required this.period,
    required this.scope,
    required this.totalCheckedIn,
    required this.noShowRatePercent,
    required this.peakHour,
    required this.peakHourSharePercent,
    required this.printedTickets,
    required this.checkinsByHour,
    required this.rows,
  });

  factory RedemptionReport.fromJson(Map<String, dynamic> json) => RedemptionReport(
        period: ReportPeriod.fromJson(json['period'] as Map<String, dynamic>),
        scope: json['scope'] as String? ?? '',
        totalCheckedIn: json['totalCheckedIn'] as int? ?? 0,
        noShowRatePercent: _toDouble(json['noShowRatePercent']),
        peakHour: json['peakHour'] as int?,
        peakHourSharePercent: _toNullableDouble(json['peakHourSharePercent']),
        printedTickets: json['printedTickets'] as int? ?? 0,
        checkinsByHour: _list(json['checkinsByHour'], CheckinHourPoint.fromJson),
        rows: _list(json['rows'], RedemptionReportRow.fromJson),
      );
}

/// One row of the Organizacije table. Half the fields are null depending on
/// [OrganizationReport.view] — the server nulls the columns the caller's role
/// has no business seeing rather than zero-filling them.
class OrganizationReportRow {
  final String organizationId;
  final String name;
  final String address;
  final int products;

  // Financial view (SuperAdmin)
  final int? tickets;
  final double? averagePrice;
  final double? growthPercent;
  final double? revenue;

  // Operational view (Admin)
  final int? published;
  final int? pending;
  final int? withoutImage;
  final bool? isPending;

  const OrganizationReportRow({
    required this.organizationId,
    required this.name,
    required this.address,
    required this.products,
    required this.tickets,
    required this.averagePrice,
    required this.growthPercent,
    required this.revenue,
    required this.published,
    required this.pending,
    required this.withoutImage,
    required this.isPending,
  });

  factory OrganizationReportRow.fromJson(Map<String, dynamic> json) => OrganizationReportRow(
        organizationId: json['organizationId'] as String? ?? '',
        name: json['name'] as String? ?? '',
        address: json['address'] as String? ?? '',
        products: json['products'] as int? ?? 0,
        tickets: json['tickets'] as int?,
        averagePrice: _toNullableDouble(json['averagePrice']),
        growthPercent: _toNullableDouble(json['growthPercent']),
        revenue: _toNullableDouble(json['revenue']),
        published: json['published'] as int?,
        pending: json['pending'] as int?,
        withoutImage: json['withoutImage'] as int?,
        isPending: json['isPending'] as bool?,
      );
}

class OrganizationReport {
  final ReportPeriod period;
  final OrganizationReportView view;
  final List<OrganizationReportRow> rows;

  const OrganizationReport({
    required this.period,
    required this.view,
    required this.rows,
  });

  factory OrganizationReport.fromJson(Map<String, dynamic> json) => OrganizationReport(
        period: ReportPeriod.fromJson(json['period'] as Map<String, dynamic>),
        view: OrganizationReportView.fromJson(json['view']),
        rows: _list(json['rows'], OrganizationReportRow.fromJson),
      );
}

/// One row of the Dashboard's "Nadolazeći događaji" card — not part of the four report tabs above
/// (no date range, no scope wrapper), returned by `GET /api/reports/upcoming-events`. `meta` is
/// already formatted server-side: "{organizacija} · {grad}" for platform staff, "{N sektora} ·
/// {grad}" for an organizer.
class UpcomingEventResponse {
  final String productId;
  final String name;
  final String meta;
  final DateTime date;
  final int sold;
  final int capacity;

  const UpcomingEventResponse({
    required this.productId,
    required this.name,
    required this.meta,
    required this.date,
    required this.sold,
    required this.capacity,
  });

  factory UpcomingEventResponse.fromJson(Map<String, dynamic> json) => UpcomingEventResponse(
        productId: json['productId'] as String,
        name: json['name'] as String? ?? '',
        meta: json['meta'] as String? ?? '',
        date: DateTime.parse(json['date'] as String),
        sold: json['sold'] as int? ?? 0,
        capacity: json['capacity'] as int? ?? 0,
      );
}

// ── Parsing helpers ─────────────────────────────────────────────────────────

/// `decimal` reaches Dart as either an `int` or a `double` depending on whether
/// the value happened to be whole, so every money/percentage field has to
/// accept both.
double _toDouble(Object? value) => (value as num?)?.toDouble() ?? 0;

/// Reads an enum sent as its integer ordinal, falling back when the server is
/// newer than this client and has appended a value we do not know yet.
T _byOrdinal<T extends Enum>(List<T> values, Object? value, T fallback) {
  final index = value is int ? value : int.tryParse('$value');
  return index != null && index >= 0 && index < values.length ? values[index] : fallback;
}

double? _toNullableDouble(Object? value) => (value as num?)?.toDouble();

List<T> _list<T>(Object? value, T Function(Map<String, dynamic>) fromJson) =>
    (value as List<dynamic>? ?? const [])
        .map((e) => fromJson(e as Map<String, dynamic>))
        .toList();

// ── AI Uvidi (GET /api/reports/insights) ───────────────────────────────────

/// Which strategy produced a block of the AI Uvidi tab.
///
/// Every block carries its own — on a young organization the forecast can
/// legitimately be [heuristic] while segmentation is [insufficient], and the UI
/// titles each block from this rather than presenting all three with the same
/// authority.
enum AnalyticsSource {
  model,
  heuristic,
  insufficient;

  static AnalyticsSource fromJson(Object? value) =>
      _byOrdinal(values, value, AnalyticsSource.insufficient);

  /// The caveat printed under a block's heading. Null for [model], which needs
  /// none.
  String? get caveat => switch (this) {
        AnalyticsSource.model => null,
        AnalyticsSource.heuristic =>
          'Procjena na osnovu prosjeka — nema dovoljno historije za model.',
        AnalyticsSource.insufficient =>
          'Nema dovoljno podataka za pouzdanu analizu u ovom periodu.',
      };
}

enum AnomalyDirection {
  spike('Skok'),
  drop('Pad');

  const AnomalyDirection(this.label);

  final String label;

  static AnomalyDirection fromJson(Object? value) =>
      _byOrdinal(values, value, AnomalyDirection.spike);
}

/// Drives an insight card's colour and its position in the list.
enum InsightSeverity {
  positive,
  neutral,
  warning,
  critical;

  static InsightSeverity fromJson(Object? value) =>
      _byOrdinal(values, value, InsightSeverity.neutral);
}

enum InsightCategory {
  forecast,
  anomaly,
  sales,
  audience,
  redemption,
  catalog;

  static InsightCategory fromJson(Object? value) =>
      _byOrdinal(values, value, InsightCategory.sales);
}

/// One day of the forecast chart. Actual days arrive with their bounds equal to
/// the value, so one widget draws both halves of the series.
class ForecastPoint {
  final DateTime date;
  final String label;
  final double revenue;
  final double lowerBound;
  final double upperBound;
  final int sold;

  const ForecastPoint({
    required this.date,
    required this.label,
    required this.revenue,
    required this.lowerBound,
    required this.upperBound,
    required this.sold,
  });

  factory ForecastPoint.fromJson(Map<String, dynamic> json) => ForecastPoint(
        date: DateTime.parse(json['date'] as String),
        label: json['label'] as String? ?? '',
        revenue: _toDouble(json['revenue']),
        lowerBound: _toDouble(json['lowerBound']),
        upperBound: _toDouble(json['upperBound']),
        sold: json['sold'] as int? ?? 0,
      );
}

class ForecastBlock {
  final AnalyticsSource source;
  final int horizon;
  final double projectedRevenue;
  final int projectedSold;

  /// Against the equally long tail of actuals. Null when that tail sold
  /// nothing — the UI drops the line rather than printing an infinity.
  final double? changePercent;

  /// The actual days the projection continues, so the chart draws one unbroken
  /// series.
  final List<ForecastPoint> actual;
  final List<ForecastPoint> points;

  const ForecastBlock({
    required this.source,
    required this.horizon,
    required this.projectedRevenue,
    required this.projectedSold,
    required this.changePercent,
    required this.actual,
    required this.points,
  });

  factory ForecastBlock.fromJson(Map<String, dynamic> json) => ForecastBlock(
        source: AnalyticsSource.fromJson(json['source']),
        horizon: json['horizon'] as int? ?? 0,
        projectedRevenue: _toDouble(json['projectedRevenue']),
        projectedSold: json['projectedSold'] as int? ?? 0,
        changePercent: (json['changePercent'] as num?)?.toDouble(),
        actual: _list(json['actual'], ForecastPoint.fromJson),
        points: _list(json['points'], ForecastPoint.fromJson),
      );
}

class SalesAnomaly {
  final DateTime date;
  final String label;
  final AnomalyDirection direction;
  final double revenue;
  final double expectedRevenue;
  final double deviationPercent;
  final double confidence;

  const SalesAnomaly({
    required this.date,
    required this.label,
    required this.direction,
    required this.revenue,
    required this.expectedRevenue,
    required this.deviationPercent,
    required this.confidence,
  });

  factory SalesAnomaly.fromJson(Map<String, dynamic> json) => SalesAnomaly(
        date: DateTime.parse(json['date'] as String),
        label: json['label'] as String? ?? '',
        direction: AnomalyDirection.fromJson(json['direction']),
        revenue: _toDouble(json['revenue']),
        expectedRevenue: _toDouble(json['expectedRevenue']),
        deviationPercent: _toDouble(json['deviationPercent']),
        confidence: _toDouble(json['confidence']),
      );
}

class AnomalyBlock {
  final AnalyticsSource source;
  final List<SalesAnomaly> items;

  const AnomalyBlock({required this.source, required this.items});

  factory AnomalyBlock.fromJson(Map<String, dynamic> json) => AnomalyBlock(
        source: AnalyticsSource.fromJson(json['source']),
        items: _list(json['items'], SalesAnomaly.fromJson),
      );
}

class AudienceSegment {
  final String name;
  final String description;
  final int buyers;
  final double sharePercent;
  final double revenueSharePercent;
  final double averageSpend;
  final double averageTickets;
  final int averageRecencyDays;

  const AudienceSegment({
    required this.name,
    required this.description,
    required this.buyers,
    required this.sharePercent,
    required this.revenueSharePercent,
    required this.averageSpend,
    required this.averageTickets,
    required this.averageRecencyDays,
  });

  factory AudienceSegment.fromJson(Map<String, dynamic> json) => AudienceSegment(
        name: json['name'] as String? ?? '',
        description: json['description'] as String? ?? '',
        buyers: json['buyers'] as int? ?? 0,
        sharePercent: _toDouble(json['sharePercent']),
        revenueSharePercent: _toDouble(json['revenueSharePercent']),
        averageSpend: _toDouble(json['averageSpend']),
        averageTickets: _toDouble(json['averageTickets']),
        averageRecencyDays: json['averageRecencyDays'] as int? ?? 0,
      );
}

class SegmentBlock {
  final AnalyticsSource source;

  /// Segmentation deliberately ignores the selected range in favour of a
  /// trailing year; this label says so, so the mismatch reads as a decision
  /// rather than a bug.
  final String windowLabel;
  final int totalBuyers;
  final List<AudienceSegment> items;

  const SegmentBlock({
    required this.source,
    required this.windowLabel,
    required this.totalBuyers,
    required this.items,
  });

  factory SegmentBlock.fromJson(Map<String, dynamic> json) => SegmentBlock(
        source: AnalyticsSource.fromJson(json['source']),
        windowLabel: json['windowLabel'] as String? ?? '',
        totalBuyers: json['totalBuyers'] as int? ?? 0,
        items: _list(json['items'], AudienceSegment.fromJson),
      );
}

/// One generated finding. [metric] arrives pre-formatted in Bosnian.
class BusinessInsight {
  final InsightSeverity severity;
  final InsightCategory category;
  final String title;
  final String body;
  final String? metric;

  const BusinessInsight({
    required this.severity,
    required this.category,
    required this.title,
    required this.body,
    required this.metric,
  });

  factory BusinessInsight.fromJson(Map<String, dynamic> json) => BusinessInsight(
        severity: InsightSeverity.fromJson(json['severity']),
        category: InsightCategory.fromJson(json['category']),
        title: json['title'] as String? ?? '',
        body: json['body'] as String? ?? '',
        metric: json['metric'] as String?,
      );
}

/// Everything the AI Uvidi tab renders.
///
/// [narrative] is null whenever no model is configured or the call failed — the
/// tab is fully usable without it, so the card is simply omitted.
class AnalyticsInsights {
  final ReportPeriod period;
  final String scope;
  final ForecastBlock forecast;
  final AnomalyBlock anomalies;
  final SegmentBlock segments;
  final List<BusinessInsight> insights;
  final String? narrative;

  const AnalyticsInsights({
    required this.period,
    required this.scope,
    required this.forecast,
    required this.anomalies,
    required this.segments,
    required this.insights,
    required this.narrative,
  });

  factory AnalyticsInsights.fromJson(Map<String, dynamic> json) => AnalyticsInsights(
        period: ReportPeriod.fromJson(json['period'] as Map<String, dynamic>),
        scope: json['scope'] as String? ?? '',
        forecast: ForecastBlock.fromJson(json['forecast'] as Map<String, dynamic>? ?? const {}),
        anomalies: AnomalyBlock.fromJson(json['anomalies'] as Map<String, dynamic>? ?? const {}),
        segments: SegmentBlock.fromJson(json['segments'] as Map<String, dynamic>? ?? const {}),
        insights: _list(json['insights'], BusinessInsight.fromJson),
        narrative: json['narrative'] as String?,
      );
}
