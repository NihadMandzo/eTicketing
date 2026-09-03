import 'dart:io';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/formatting.dart';
import '../main.dart';
import '../models/responses/report_responses.dart';
import '../models/responses/user_profile.dart';
import '../providers/api_exception.dart';
import '../providers/report_provider.dart';
import '../theme/app_colors.dart';
import 'widgets/reports/report_bar_chart.dart';
import 'widgets/reports/report_data_table.dart';
import 'widgets/reports/report_insight_card.dart';
import 'widgets/reports/report_metric_card.dart';
import 'widgets/reports/report_narrative_card.dart';
import 'widgets/reports/report_range_bar.dart';
import 'widgets/reports/report_segment_card.dart';

/// Izvještaji — the back-office reporting screen from docs/Design/Reports.dc.html.
///
/// Which tabs exist, what data they cover and whether the export button appears
/// all depend on the signed-in role. The matrix is duplicated here and in
/// `ReportService.Authorize` on purpose: this copy shapes the UI, that one is
/// the authorization (.claude/rules/21-frontend-desktop.md — hiding a tab is
/// not a permission check, and every request is refused server-side too).
///
/// The design's yellow "Pregled kao:" role switcher is deliberately not built —
/// it is a mock-up affordance for demonstrating all four roles on one page, not
/// a feature; the real screen reads the role from the session.
class ReportsScreen extends StatefulWidget {
  final UserProfile user;

  const ReportsScreen({super.key, required this.user});

  @override
  State<ReportsScreen> createState() => _ReportsScreenState();
}

class _ReportsScreenState extends State<ReportsScreen> {
  final ReportProvider _provider = ReportProvider();

  late List<ReportTab> _tabs;
  late ReportTab _tab;

  late DateTime _from;
  late DateTime _to;
  String? _presetId = '30d';

  /// The last range that actually produced data. An inverted range is shown as
  /// a warning while the figures on screen stay on the last valid period,
  /// rather than blanking the page — the behaviour the design specifies.
  late DateTime _loadedFrom;
  late DateTime _loadedTo;

  bool _isLoading = true;
  bool _isExporting = false;
  String? _errorMessage;

  /// Incremented on every [_load]. A response whose generation no longer matches
  /// has been superseded and is discarded — see [_load].
  int _loadGeneration = 0;

  SalesReport? _sales;
  ProductReport? _products;
  RedemptionReport? _redemption;
  OrganizationReport? _organizations;
  AnalyticsInsights? _insights;

  /// Forecast horizon for the AI Uvidi tab. The three the API accepts — see
  /// `InsightsQueryValidator`, which refuses anything else. Selected from the
  /// heading of the Prognoza prihoda card (see `_horizonSelector`), beside the
  /// chart it redraws.
  int _horizon = 14;

  /// True while a horizon change is being applied.
  ///
  /// Deliberately separate from `_isLoading`: that flag blanks the whole tab
  /// behind a spinner, which is the "full refresh" a horizon change must not
  /// cause — findings, anomalies and segments don't depend on the horizon and
  /// have no reason to disappear while only the forecast re-fetches. See
  /// `_changeHorizon`.
  bool _isRefreshingInsights = false;

  /// The tabs each role may see — the client half of the matrix in
  /// `ReportService.Authorize`.
  ///
  /// AI Uvidi sits with Prodaja: it forecasts and segments money, so Admin —
  /// whose remit is operational — does not get it, exactly as for Prodaja.
  static const _tabsByRole = <String, List<ReportTab>>{
    'SuperAdmin': [
      ReportTab.sales,
      ReportTab.products,
      ReportTab.redemption,
      ReportTab.organizations,
      ReportTab.insights,
    ],
    'Admin': [ReportTab.products, ReportTab.organizations],
    'OrganizationSuperAdmin': [
      ReportTab.sales,
      ReportTab.products,
      ReportTab.redemption,
      ReportTab.insights,
    ],
    'OrganizationAdmin': [ReportTab.sales, ReportTab.products, ReportTab.insights],
  };

  /// Every staff role but OrganizationAdmin may carry a report out of the app.
  bool get _canExport => widget.user.roleName != 'OrganizationAdmin';

  bool get _isDark => Theme.of(context).brightness == Brightness.dark;

  DateTime get _today {
    final now = DateTime.now();
    return DateTime(now.year, now.month, now.day);
  }

  /// Mirrors the backend's `ReportRangeValidator`: inverted or longer than a
  /// year. Loading is skipped while it holds, so the figures on screen stay on
  /// the last period that was actually valid rather than blanking.
  bool get _rangeInvalid =>
      _from.isAfter(_to) || _to.difference(_from).inDays + 1 > ReportRangeBar.maxRangeDays;

  @override
  void initState() {
    super.initState();

    _tabs = _tabsByRole[widget.user.roleName] ?? const [ReportTab.products];
    _tab = _tabs.first;

    final (from, to) = const ReportPreset('30d', '30 dana', days: 30).resolve(DateTime.now());
    _from = from;
    _to = to;
    _loadedFrom = from;
    _loadedTo = to;

    _load();
  }

  // ── Loading ───────────────────────────────────────────────────────────────

  /// Fetches only the active tab. The four reports are four separate queries
  /// server-side, and a platform-wide yearly range is not cheap — loading the
  /// three the user is not looking at would triple that for nothing.
  Future<void> _load() async {
    if (_rangeInvalid) return;

    // Supersedes any request still in flight. The user can change tab, preset or
    // date faster than a wide range answers, and a wider range is the slower one
    // — so without this the older response is actually the likelier to land last
    // and paint stale figures under the newly selected filter.
    final requestId = ++_loadGeneration;

    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      switch (_tab) {
        case ReportTab.sales:
          _sales = await _provider.getSales(_from, _to);
          break;
        case ReportTab.products:
          _products = await _provider.getProducts(_from, _to);
          break;
        case ReportTab.redemption:
          _redemption = await _provider.getRedemption(_from, _to);
          break;
        case ReportTab.organizations:
          _organizations = await _provider.getOrganizations(_from, _to);
          break;
        case ReportTab.insights:
          _insights = await _provider.getInsights(_from, _to, horizon: _horizon);
          break;
      }

      if (!mounted || requestId != _loadGeneration) return;
      setState(() {
        _loadedFrom = _from;
        _loadedTo = _to;
        _isLoading = false;
      });
    } on ApiException catch (e) {
      if (!mounted || requestId != _loadGeneration) return;
      setState(() {
        _errorMessage = e.apiError.displayMessage;
        _isLoading = false;
      });
    } catch (_) {
      if (!mounted || requestId != _loadGeneration) return;
      setState(() {
        _errorMessage = 'Izvještaj nije moguće učitati. Pokušajte ponovo.';
        _isLoading = false;
      });
    }
  }

  /// Re-fetches AI Uvidi for a new horizon without the tab's full loading
  /// spinner — the "full refresh" the horizon control must not cause. The
  /// selected chip shows its own small spinner instead (see
  /// `_horizonSelector`), while everything else already on screen — tiles,
  /// findings, anomalies, segments — stays exactly as it is, since none of it
  /// depends on the horizon.
  ///
  /// Shares `_loadGeneration` with `_load()` rather than a horizon-local
  /// counter: a date change or tab switch mid-flight has to supersede this
  /// fetch exactly the way it supersedes any other one, and one guard is what
  /// makes that automatic.
  Future<void> _changeHorizon(int horizon) async {
    if (_horizon == horizon || _isRefreshingInsights) return;

    final requestId = ++_loadGeneration;
    setState(() {
      _horizon = horizon;
      _isRefreshingInsights = true;
    });

    try {
      final result = await _provider.getInsights(_from, _to, horizon: horizon);
      if (!mounted || requestId != _loadGeneration) return;
      setState(() {
        _insights = result;
        _isRefreshingInsights = false;
      });
    } catch (e) {
      if (!mounted || requestId != _loadGeneration) return;
      setState(() => _isRefreshingInsights = false);
      handleApiError(e);
    }
  }

  void _selectTab(int index) {
    if (_tabs[index] == _tab) return;
    setState(() => _tab = _tabs[index]);
    _load();
  }

  void _applyPreset(ReportPreset preset) {
    final (from, to) = preset.resolve(DateTime.now());
    setState(() {
      _presetId = preset.id;
      _from = from;
      _to = to;
    });
    _load();
  }

  void _setFrom(DateTime value) {
    setState(() {
      _presetId = null;
      _from = value;
    });
    _load();
  }

  void _setTo(DateTime value) {
    setState(() {
      _presetId = null;
      _to = value;
    });
    _load();
  }

  // ── Export ────────────────────────────────────────────────────────────────

  /// Asks the server to render the active tab, then writes the bytes wherever
  /// the user points the save dialog — the same flow as
  /// `saveTicketExport`, including its cancel-is-not-an-error behaviour.
  Future<void> _export() async {
    // Matches the name the server puts in Content-Disposition, so the dialog
    // suggests what the file would have been called anyway.
    final suggested =
        'izvjestaj-${_tabSlug(_tab)}-'
        '${_isoDate(_loadedFrom)}-${_isoDate(_loadedTo)}.pdf';

    final path = await FilePicker.platform.saveFile(
      dialogTitle: 'Sačuvaj izvještaj kao PDF',
      fileName: suggested,
      type: FileType.custom,
      allowedExtensions: const ['pdf'],
    );
    if (path == null) return;

    setState(() => _isExporting = true);
    try {
      final bytes = await _provider.downloadPdf(_tab, _loadedFrom, _loadedTo);
      await File(path).writeAsBytes(bytes, flush: true);
      handleApiSuccess('Izvještaj je sačuvan.');
    } catch (e) {
      handleApiError(e);
    } finally {
      if (mounted) setState(() => _isExporting = false);
    }
  }

  // ── Build ─────────────────────────────────────────────────────────────────

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _header(),
          const SizedBox(height: 20),
          ReportRangeBar(
            from: _from,
            to: _to,
            activePresetId: _presetId,
            today: _today,
            onPreset: _applyPreset,
            onFrom: _setFrom,
            onTo: _setTo,
            // Sits with the period controls rather than up in the page header:
            // what gets exported is whatever range is selected right here, so
            // the button belongs next to the thing that decides it.
            trailing: _rangeBarTrailing(),
          ),
          if (_rangeInvalid) ...[
            const SizedBox(height: 10),
            Text(
              'Prikazuju se podaci za posljednji ispravan period: '
              '${formatLongDate(_loadedFrom)} – ${formatLongDate(_loadedTo)}.',
              style: TextStyle(fontSize: 12, color: AppColors.textTertiary(Theme.of(context).brightness)),
            ),
          ],
          const SizedBox(height: 22),
          ReportTabBar(
            labels: [for (final tab in _tabs) tab.label],
            selectedIndex: _tabs.indexOf(_tab),
            onSelect: _selectTab,
          ),
          const SizedBox(height: 22),
          _body(),
        ],
      ),
    );
  }

  Widget _header() {
    final brightness = Theme.of(context).brightness;

    return ConstrainedBox(
      constraints: const BoxConstraints(maxWidth: 620),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          const Text(
            'Izvještaji',
            style: TextStyle(fontSize: 28, fontWeight: FontWeight.w700, letterSpacing: -0.4),
          ),
          const SizedBox(height: 6),
          Text(_subtitle, style: TextStyle(fontSize: 14, color: AppColors.textTertiary(brightness))),
        ],
      ),
    );
  }

  /// The export action, rendered by the range bar so it sits against the card's
  /// right edge beside the dates it exports.
  Widget _exportButton() => FilledButton.icon(
    onPressed: (_isExporting || _isLoading) ? null : _export,
    icon: _isExporting
        ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2))
        : const Icon(LucideIcons.fileDown, size: 16),
    label: Text(_isExporting ? 'Izvoz...' : 'Izvezi PDF'),
  );

  /// The one thing pinned to the range bar's right edge: the export button
  /// every exporting role gets on every tab.
  ///
  /// The AI Uvidi forecast horizon used to sit here too and deliberately no
  /// longer does. Three chips reading "7 d · 14 d · 30 d" immediately beside the
  /// bar's own "7 dana · 30 dana · 90 dana" date presets are one control group
  /// to the eye and two different things in fact — one picks the period being
  /// reported on, the other how far past it to project. The horizon now sits in
  /// the heading of the Prognoza prihoda card, which is the only thing it
  /// changes.
  Widget? _rangeBarTrailing() => _canExport ? _exportButton() : null;

  /// The design's per-role subtitle: what this role's copy of the screen is for.
  String get _subtitle => switch (widget.user.roleName) {
    'SuperAdmin' => 'Puni uvid u prodaju, organizacije i iskorištenost karata na platformi',
    'Admin' => 'Operativni izvještaji — učinak proizvoda i organizacija',
    'OrganizationSuperAdmin' => 'Prodaja, popunjenost i iskorištenost karata vaše organizacije',
    _ => 'Pregled prodaje i učinka proizvoda vaše organizacije',
  };

  Widget _body() {
    if (_isLoading) {
      return const Padding(
        padding: EdgeInsets.symmetric(vertical: 64),
        child: Center(child: CircularProgressIndicator()),
      );
    }

    if (_errorMessage != null) {
      return Padding(
        padding: const EdgeInsets.symmetric(vertical: 48),
        child: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                _errorMessage!,
                textAlign: TextAlign.center,
                style: TextStyle(color: AppColors.textTertiary(Theme.of(context).brightness)),
              ),
              const SizedBox(height: 12),
              OutlinedButton(onPressed: _load, child: const Text('Pokušaj ponovo')),
            ],
          ),
        ),
      );
    }

    return switch (_tab) {
      ReportTab.sales => _salesTab(),
      ReportTab.products => _productsTab(),
      ReportTab.redemption => _redemptionTab(),
      ReportTab.organizations => _organizationsTab(),
      ReportTab.insights => _insightsTab(),
    };
  }

  // ── Prodaja ───────────────────────────────────────────────────────────────

  Widget _salesTab() {
    final report = _sales;
    if (report == null) return const SizedBox.shrink();

    final peak = report.buckets.isEmpty
        ? 0.0
        : report.buckets.map((b) => b.revenue).reduce((a, b) => a > b ? a : b);

    final chart = ReportCard(
      title: 'Prihod ${report.period.bucketUnit.title}',
      child: ReportBarChart(
        bars: [
          for (final bucket in report.buckets)
            ReportBarData(
              label: bucket.label,
              value: _compactMoney(bucket.revenue),
              ratio: peak == 0 ? 0 : bucket.revenue / peak,
            ),
        ],
      ),
    );

    final tiles = [
      ReportMetricCard(
        label: 'Ukupan prihod',
        value: formatMoney(report.grossRevenue),
        hint: report.revenueChangePercent == null
            ? 'Nema podataka za prethodni period'
            : '${formatSignedPercent(report.revenueChangePercent)} u odnosu na prethodni period',
        emphasis: ReportEmphasis.positive,
      ),
      ReportMetricCard(label: 'Prodanih karata', value: formatCount(report.ticketsSold)),
      ReportMetricCard(label: 'Prosječna cijena karte', value: formatMoney(report.averageTicketPrice)),
    ];

    // Platform-wide reports only: the backend sends an empty list to an organizer, whose report
    // is already scoped to one organization, and the section is dropped rather than showing a
    // single row restating the tiles above it.
    final byOrganization = report.byOrganization.isEmpty ? null : _salesByOrganizationCard(report);

    return LayoutBuilder(
      builder: (context, constraints) {
        // Below ~1100px the design's 1.6fr/1fr split leaves the chart too narrow
        // to read, so the two columns stack instead
        // (.claude/rules/21-frontend-desktop.md).
        final isWide = constraints.maxWidth >= 1100;

        if (!isWide) {
          return Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              _tileGrid(tiles, constraints.maxWidth),
              const SizedBox(height: 16),
              chart,
              if (byOrganization != null) ...[const SizedBox(height: 16), byOrganization],
            ],
          );
        }

        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(flex: 16, child: chart),
                const SizedBox(width: 20),
                Expanded(
                  flex: 10,
                  child: Column(
                    children: [
                      for (final tile in tiles) ...[
                        SizedBox(width: double.infinity, child: tile),
                        if (tile != tiles.last) const SizedBox(height: 16),
                      ],
                    ],
                  ),
                ),
              ],
            ),
            // Full width below the chart+tiles row: it is a breakdown of the same revenue, and
            // five columns do not read at the tile column's width.
            if (byOrganization != null) ...[const SizedBox(height: 20), byOrganization],
          ],
        );
      },
    );
  }

  /// Splits the headline revenue by owning organization. Rows sum to the "Ukupan prihod" tile —
  /// the backend computes `sharePercent` against that same figure so the two cannot disagree.
  Widget _salesByOrganizationCard(SalesReport report) {
    final rows = report.byOrganization;

    return ReportCard(
      title: 'Prodaja po organizacijama',
      subtitle: '${formatCount(rows.length)} '
          '${rows.length == 1 ? 'organizacija' : 'organizacija'} sa prodajom u periodu',
      child: ReportDataTable(
        columns: const [
          ReportColumn('Organizacija', flex: 30),
          ReportColumn('Prodano', flex: 12, rightAligned: true),
          ReportColumn('Pros. cijena', flex: 14, rightAligned: true),
          ReportColumn('Udio', flex: 12, rightAligned: true),
          ReportColumn('Prihod', flex: 16, rightAligned: true),
        ],
        rows: [
          for (final row in rows)
            [
              ReportCell(row.name),
              ReportCell(formatCount(row.sold)),
              ReportCell(formatMoney(row.averagePrice)),
              ReportCell(formatPercent(row.sharePercent)),
              ReportCell(formatMoney(row.revenue), color: _positive, bold: true),
            ],
        ],
        totalsRow: [
          const ReportCell('Ukupno'),
          ReportCell(formatCount(report.ticketsSold)),
          ReportCell(formatMoney(report.averageTicketPrice)),
          ReportCell(formatPercent(100)),
          ReportCell(formatMoney(report.grossRevenue), color: _positive, bold: true),
        ],
      ),
    );
  }

  // ── Učinak proizvoda ──────────────────────────────────────────────────────

  Widget _productsTab() {
    final report = _products;
    if (report == null) return const SizedBox.shrink();

    return LayoutBuilder(
      builder: (context, constraints) => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _tileGrid([
            ReportMetricCard(label: 'Prodanih karata', value: formatCount(report.totalSold)),
            ReportMetricCard(
              label: 'Ukupan prihod',
              value: formatMoney(report.totalRevenue),
              emphasis: ReportEmphasis.positive,
            ),
            ReportMetricCard(label: 'Prosječna cijena', value: formatMoney(report.averagePrice)),
            ReportMetricCard(
              label: 'Prosječna popunjenost',
              value: formatPercent(report.averageOccupancyPercent),
            ),
          ], constraints.maxWidth),
          const SizedBox(height: 16),
          ReportCard(
            title: 'Učinak proizvoda',
            subtitle:
                '${formatCount(report.rows.length)} '
                '${report.rows.length == 1 ? 'proizvod' : 'proizvoda'} · ${report.scope}',
            child: ReportDataTable(
              columns: const [
                ReportColumn('Proizvod', flex: 28),
                ReportColumn('Prodano', flex: 10, rightAligned: true),
                ReportColumn('Popunjenost', flex: 15),
                ReportColumn('Pros. cijena', flex: 13, rightAligned: true),
                ReportColumn('Prihod', flex: 14, rightAligned: true),
              ],
              rows: [
                for (final row in report.rows)
                  [
                    ReportCell.custom(ReportNameCell(name: row.name, meta: row.meta)),
                    ReportCell(formatCount(row.sold)),
                    ReportCell.custom(
                      ReportProgressCell(
                        percent: row.occupancyPercent,
                        color: _occupancyColor(row.occupancyPercent),
                        label: formatPercent(row.occupancyPercent),
                      ),
                    ),
                    ReportCell(formatMoney(row.averagePrice)),
                    ReportCell(formatMoney(row.revenue), color: _positive, bold: true),
                  ],
              ],
              totalsRow: [
                const ReportCell('Ukupno'),
                ReportCell(formatCount(report.totalSold)),
                ReportCell('${formatPercent(report.averageOccupancyPercent)} pros.'),
                ReportCell(formatMoney(report.averagePrice)),
                ReportCell(formatMoney(report.totalRevenue), color: _positive),
              ],
            ),
          ),
        ],
      ),
    );
  }

  // ── Iskorištenost karata ──────────────────────────────────────────────────

  Widget _redemptionTab() {
    final report = _redemption;
    if (report == null) return const SizedBox.shrink();

    final peak = report.checkinsByHour.isEmpty
        ? 0
        : report.checkinsByHour.map((h) => h.count).reduce((a, b) => a > b ? a : b);

    return LayoutBuilder(
      builder: (context, constraints) => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _tileGrid([
            ReportMetricCard(
              label: 'Ukupno skenirano',
              value: formatCount(report.totalCheckedIn),
              hint: 'Na ulazima',
              icon: LucideIcons.scanLine,
            ),
            ReportMetricCard(
              label: 'Stopa nedolaska',
              value: formatPercent(report.noShowRatePercent),
              hint: 'Prodane, neiskorištene karte',
              emphasis: ReportEmphasis.negative,
              icon: LucideIcons.userX,
            ),
            ReportMetricCard(
              label: 'Vrhunac dolaska',
              value: formatHourWindow(report.peakHour),
              hint: report.peakHourSharePercent == null
                  ? null
                  : '${formatPercent(report.peakHourSharePercent)} svih skeniranja',
              icon: LucideIcons.clock,
            ),
            ReportMetricCard(
              label: 'Štampane karte',
              value: formatCount(report.printedTickets),
              hint: 'Prodane na blagajni',
              icon: LucideIcons.printer,
            ),
          ], constraints.maxWidth),
          const SizedBox(height: 16),
          ReportCard(
            title: 'Dolazak po satu',
            subtitle: 'Skeniranja na ulazima · ${report.scope}',
            child: ReportBarChart(
              height: 140,
              highlightPeak: true,
              bars: [
                for (final hour in report.checkinsByHour)
                  ReportBarData(
                    label: '${hour.hour.toString().padLeft(2, '0')}h',
                    value: formatCount(hour.count),
                    ratio: peak == 0 ? 0 : hour.count / peak,
                  ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          ReportCard(
            title: 'Iskorištenost karata (check-in)',
            child: ReportDataTable(
              columns: const [
                ReportColumn('Događaj', flex: 24),
                ReportColumn('Prodano', flex: 10, rightAligned: true),
                ReportColumn('Iskorišteno', flex: 10, rightAligned: true),
                ReportColumn('Nije došlo', flex: 10, rightAligned: true),
                ReportColumn('Štampane', flex: 10, rightAligned: true),
                ReportColumn('Stopa', flex: 10),
              ],
              rows: [
                for (final row in report.rows)
                  [
                    ReportCell.custom(ReportNameCell(name: row.name)),
                    ReportCell(formatCount(row.sold)),
                    ReportCell(formatCount(row.checkedIn)),
                    ReportCell(formatCount(row.noShow), color: row.noShow > 0 ? AppColors.error : null),
                    ReportCell(formatCount(row.printed)),
                    ReportCell.custom(
                      ReportBadge(
                        label: formatPercent(row.ratePercent),
                        color: row.ratePercent >= 85 ? _positive : AppColors.warningDark,
                      ),
                    ),
                  ],
              ],
            ),
          ),
        ],
      ),
    );
  }

  // ── Organizacije ──────────────────────────────────────────────────────────

  Widget _organizationsTab() {
    final report = _organizations;
    if (report == null) return const SizedBox.shrink();

    final financial = report.view == OrganizationReportView.financial;

    return ReportCard(
      title: 'Učinak organizacija',
      subtitle:
          '${formatCount(report.rows.length)} '
          '${report.rows.length == 1 ? 'organizacija' : 'organizacija'} na platformi',
      child: ReportDataTable(
        columns: financial
            ? const [
                ReportColumn('Organizacija', flex: 24),
                ReportColumn('Proizvodi', flex: 10, rightAligned: true),
                ReportColumn('Karte', flex: 10, rightAligned: true),
                ReportColumn('Pros. cijena', flex: 12, rightAligned: true),
                ReportColumn('Rast', flex: 10, rightAligned: true),
                ReportColumn('Prihod', flex: 13, rightAligned: true),
              ]
            : const [
                ReportColumn('Organizacija', flex: 24),
                ReportColumn('Proizvodi', flex: 10, rightAligned: true),
                ReportColumn('Objavljeno', flex: 10, rightAligned: true),
                ReportColumn('Na čekanju', flex: 10, rightAligned: true),
                ReportColumn('Bez slike', flex: 10, rightAligned: true),
                ReportColumn('Status', flex: 12),
              ],
        rows: [
          for (final row in report.rows)
            financial
                ? [
                    ReportCell.custom(ReportNameCell(name: row.name, meta: row.address)),
                    ReportCell(formatCount(row.products)),
                    ReportCell(formatCount(row.tickets ?? 0)),
                    ReportCell(formatMoney(row.averagePrice ?? 0)),
                    ReportCell(
                      formatSignedPercent(row.growthPercent),
                      color: row.growthPercent == null
                          ? null
                          : (row.growthPercent! < 0 ? AppColors.error : _positive),
                      bold: true,
                    ),
                    ReportCell(formatMoney(row.revenue ?? 0), color: _positive, bold: true),
                  ]
                : [
                    ReportCell.custom(ReportNameCell(name: row.name, meta: row.address)),
                    ReportCell(formatCount(row.products)),
                    ReportCell(formatCount(row.published ?? 0)),
                    ReportCell(
                      formatCount(row.pending ?? 0),
                      color: (row.pending ?? 0) > 0 ? AppColors.warningDark : null,
                    ),
                    ReportCell(formatCount(row.withoutImage ?? 0)),
                    ReportCell.custom(
                      ReportBadge(
                        label: row.isPending == true ? 'Na čekanju' : 'Aktivna',
                        color: row.isPending == true ? AppColors.warningDark : _positive,
                      ),
                    ),
                  ],
        ],
      ),
    );
  }

  // ── AI Uvidi ──────────────────────────────────────────────────────────────

  /// The AI tab: an optional generated summary, the ranked findings, the
  /// forecast chart, the anomaly table and the audience segments.
  ///
  /// Every block prints the strategy that produced it. A forecast fitted from
  /// four weeks of history and one extrapolated from a fortnight's average are
  /// not the same claim, and a screen that presents them identically is lying
  /// by omission — see `AnalyticsSource.caveat`.
  Widget _insightsTab() {
    final report = _insights;
    if (report == null) return const SizedBox.shrink();

    return LayoutBuilder(
      builder: (context, constraints) => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _tileGrid([
            ReportMetricCard(
              label: 'Projekcija prihoda',
              value: formatMoney(report.forecast.projectedRevenue),
              hint: report.forecast.changePercent == null
                  ? 'Narednih ${report.forecast.horizon} dana'
                  : '${formatSignedPercent(report.forecast.changePercent)} '
                        'u odnosu na prethodnih ${report.forecast.horizon} dana',
              // Only a projected fall is coloured. A rise stays neutral: it is a
              // projection, and painting it green reads as money already earned.
              emphasis: (report.forecast.changePercent ?? 0) < 0
                  ? ReportEmphasis.negative
                  : ReportEmphasis.neutral,
            ),
            ReportMetricCard(
              label: 'Projekcija karata',
              value: formatCount(report.forecast.projectedSold),
              hint: 'Narednih ${report.forecast.horizon} dana',
            ),
            ReportMetricCard(
              label: 'Neuobičajenih dana',
              value: formatCount(report.anomalies.items.length),
              hint: report.anomalies.source == AnalyticsSource.insufficient
                  ? 'Nema dovoljno podataka'
                  : 'U odabranom periodu',
            ),
            ReportMetricCard(
              label: 'Kupaca u analizi',
              value: formatCount(report.segments.totalBuyers),
              hint: report.segments.windowLabel,
            ),
          ], constraints.maxWidth),
          if (report.narrative != null) ...[
            const SizedBox(height: 16),
            ReportNarrativeCard(narrative: report.narrative!),
          ],
          const SizedBox(height: 16),
          _insightsAndForecastRow(report, constraints.maxWidth),
          const SizedBox(height: 16),
          _anomaliesAndSegmentsRow(report, constraints.maxWidth),
        ],
      ),
    );
  }

  /// The anomaly table and the audience segments share the last row on a window
  /// wide enough for both.
  ///
  /// Both are bounded server-side — at most five anomalies
  /// (`SsaAnomalyDetector.MaxAnomalies`) and four segments
  /// (`KMeansAudienceSegmenter.ClusterCount`) — so the two cards come out close
  /// to the same height, and neither has any use for the full width of a
  /// maximised window: a five-column table stretched that far leaves a hand-span
  /// of nothing between a date and its figure.
  ///
  /// The condition is measured, not a round breakpoint, because getting it wrong
  /// costs more than the row is worth: too narrow a table scrolls sideways
  /// inside its own card, and too narrow a segment column collapses to a single
  /// stack of tall cards beside a short table — both worse than the full-width
  /// stack this falls back to.
  Widget _anomaliesAndSegmentsRow(AnalyticsInsights report, double width) {
    final segments = _segmentsCard(report);

    // Nothing unusual in the period: the segments take the row on their own,
    // and lay themselves out against the width they are actually given.
    if (report.anomalies.items.isEmpty) return segments;

    const gap = 16.0;
    // ReportCard's default padding, on both edges.
    const cardPadding = 40.0;
    final anomaliesWidth = (width - gap) * 2 / 5 - cardPadding;
    final segmentsWidth = (width - gap) * 3 / 5 - cardPadding;

    final segmentCount = report.segments.items.length;
    final fits = anomaliesWidth >= _anomalyTableMinWidth &&
        (segmentCount <= 1 || ReportSegmentCard.columnsFor(segmentCount, segmentsWidth) > 1);

    final anomalies = _anomaliesCard(report);
    if (!fits) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [anomalies, const SizedBox(height: 16), segments],
      );
    }

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(flex: 2, child: anomalies),
        const SizedBox(width: 16),
        Expanded(flex: 3, child: segments),
      ],
    );
  }

  /// Where the anomaly table stops shrinking and scrolls sideways instead.
  ///
  /// Lower than `ReportDataTable`'s 720px default, which is set for the seven
  /// columns of the product and organization tables. This one has five, the
  /// widest of them holding a figure like `1.469.555,00 KM` — 620 keeps that
  /// off the ellipsis and lets the table sit in the narrower of the two columns
  /// on this row.
  static const double _anomalyTableMinWidth = 620;

  /// Poslovni uvidi and the forecast chart share one row on a wide window: the
  /// chart is deliberately the narrower of the two — its bars scroll
  /// horizontally once they need more width than they are given
  /// (`ReportBarChart` already does this) — so the findings card gets the room
  /// it needs to lay its own cards two-across instead of stacking one long
  /// single-file column.
  Widget _insightsAndForecastRow(AnalyticsInsights report, double width) {
    final insights = _insightsCard(report);
    final forecast = _forecastCard(report);

    // Same breakpoint the rest of this screen uses for a two-column split
    // (see the Prodaja tab). Below it, a squeezed findings column and a
    // horizontally-scrolling chart would both be fighting for the same narrow
    // space, so they stack instead.
    if (width < 1100) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [insights, const SizedBox(height: 16), forecast],
      );
    }

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(flex: 3, child: insights),
        const SizedBox(width: 16),
        Expanded(flex: 2, child: forecast),
      ],
    );
  }

  Widget _insightsCard(AnalyticsInsights report) => ReportCard(
    title: 'Poslovni uvidi',
    subtitle: '${formatCount(report.insights.length)} '
        '${report.insights.length == 1 ? 'nalaz' : 'nalaza'} · ${report.scope}',
    // Wrapped into columns rather than one long stack: this card sits beside
    // the forecast chart and is already narrower than the tab itself, so the
    // cards read in multiple lines instead of a single tall column.
    child: LayoutBuilder(
      builder: (context, constraints) {
        const gap = 10.0;
        final columns = constraints.maxWidth >= 480 ? 2 : 1;
        final cardWidth = columns == 1 ? constraints.maxWidth : (constraints.maxWidth - gap) / 2;

        return Wrap(
          spacing: gap,
          runSpacing: gap,
          children: [
            for (final insight in report.insights)
              SizedBox(width: cardWidth, child: ReportInsightCard(insight: insight)),
          ],
        );
      },
    ),
  );

  /// Actuals and projection drawn as one series. Both halves are scaled against
  /// the same peak, or the join would show a step that is an artefact of the
  /// drawing rather than of the data.
  Widget _forecastCard(AnalyticsInsights report) {
    final forecast = report.forecast;
    final bars = [...forecast.actual, ...forecast.points];
    final peak = bars.isEmpty ? 0.0 : bars.map((p) => p.revenue).reduce((a, b) => a > b ? a : b);

    return ReportCard(
      title: 'Prognoza prihoda',
      subtitle: forecast.source.caveat ??
          'Model vremenske serije (SSA) · projekcija za ${forecast.horizon} dana',
      trailing: _horizonSelector(),
      child: forecast.points.isEmpty
          ? _emptyBlock('Nema dovoljno historijskih podataka za prognozu u ovom periodu.')
          : Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                ReportBarChart(
                  bars: [
                    for (final point in forecast.actual)
                      ReportBarData(
                        label: point.label,
                        // A day with no sales prints nothing above its empty
                        // slot. A row of zeroes over flat baseline is noise the
                        // reader has to filter out to find the days that
                        // actually carry a figure.
                        value: point.revenue == 0 ? '' : _compactMoney(point.revenue),
                        ratio: peak == 0 ? 0 : point.revenue / peak,
                      ),
                    for (final point in forecast.points)
                      ReportBarData(
                        label: point.label,
                        value: point.revenue == 0 ? '' : _compactMoney(point.revenue),
                        ratio: peak == 0 ? 0 : point.revenue / peak,
                        isProjected: true,
                      ),
                  ],
                ),
                const SizedBox(height: 12),
                _forecastLegend(forecast),
              ],
            ),
    );
  }

  /// Explains the hollow bars, and states the confidence band in words — the
  /// chart cannot draw the interval at this bar width, and a projection shown
  /// without its uncertainty reads as a promise.
  Widget _forecastLegend(ForecastBlock forecast) {
    final brightness = Theme.of(context).brightness;
    final lower = forecast.points.fold<double>(0, (sum, p) => sum + p.lowerBound);
    final upper = forecast.points.fold<double>(0, (sum, p) => sum + p.upperBound);

    return Wrap(
      spacing: 18,
      runSpacing: 8,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 14,
              height: 10,
              decoration: BoxDecoration(
                gradient: const LinearGradient(
                  begin: Alignment.topCenter,
                  end: Alignment.bottomCenter,
                  colors: [AppColors.secondary, AppColors.primary],
                ),
                borderRadius: BorderRadius.circular(3),
              ),
            ),
            const SizedBox(width: 6),
            Text(
              'Ostvareno',
              style: TextStyle(fontSize: 12, color: AppColors.textTertiary(brightness)),
            ),
          ],
        ),
        Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 14,
              height: 10,
              decoration: BoxDecoration(
                border: Border.all(color: AppColors.secondary.withValues(alpha: 0.75), width: 1.4),
                borderRadius: BorderRadius.circular(3),
              ),
            ),
            const SizedBox(width: 6),
            Text(
              'Projekcija',
              style: TextStyle(fontSize: 12, color: AppColors.textTertiary(brightness)),
            ),
          ],
        ),
        Text(
          'Interval pouzdanosti 95%: ${formatMoney(lower)} – ${formatMoney(upper)}',
          style: TextStyle(fontSize: 12, color: AppColors.textTertiary(brightness)),
        ),
      ],
    );
  }

  /// 7 / 14 / 30, the only horizons the API accepts. Sits in the heading of the
  /// Prognoza prihoda card, against the chart and the caption that both restate
  /// the number it holds — see `_rangeBarTrailing` for why it is no longer up in
  /// the period bar next to the date presets. Goes through `_changeHorizon`,
  /// which re-fetches quietly instead of the tab's full loading spinner; the
  /// selected chip shows its own small spinner while that is in flight.
  Widget _horizonSelector() {
    final brightness = Theme.of(context).brightness;

    return Wrap(
      spacing: 6,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        for (final horizon in const [7, 14, 30])
          ChoiceChip(
            avatar: (_isRefreshingInsights && _horizon == horizon)
                ? const SizedBox(
                    width: 12,
                    height: 12,
                    child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                  )
                : null,
            label: Text('$horizon d'),
            selected: _horizon == horizon,
            labelStyle: TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w600,
              color: _horizon == horizon ? Colors.white : AppColors.textSecondary(brightness),
            ),
            selectedColor: AppColors.primary,
            showCheckmark: false,
            onSelected: (_isLoading || _isRefreshingInsights) ? null : (_) => _changeHorizon(horizon),
          ),
      ],
    );
  }

  Widget _anomaliesCard(AnalyticsInsights report) => ReportCard(
    title: 'Neuobičajeni dani',
    subtitle: report.anomalies.source.caveat ??
        'Dani koji odstupaju od trenda i sedmičnog ritma prodaje',
    child: ReportDataTable(
      minWidth: _anomalyTableMinWidth,
      columns: const [
        ReportColumn('Datum', flex: 16),
        ReportColumn('Vrsta', flex: 12),
        ReportColumn('Prihod', flex: 18, rightAligned: true),
        ReportColumn('Očekivano', flex: 18, rightAligned: true),
        ReportColumn('Odstupanje', flex: 16, rightAligned: true),
      ],
      rows: [
        for (final anomaly in report.anomalies.items)
          [
            ReportCell(anomaly.label),
            ReportCell.custom(
              ReportBadge(
                label: anomaly.direction.label,
                color: anomaly.direction == AnomalyDirection.spike ? _positive : AppColors.warningDark,
              ),
            ),
            ReportCell(formatMoney(anomaly.revenue), bold: true),
            ReportCell(formatMoney(anomaly.expectedRevenue)),
            ReportCell(
              formatSignedPercent(anomaly.deviationPercent),
              color: anomaly.direction == AnomalyDirection.spike ? _positive : AppColors.warningDark,
              bold: true,
            ),
          ],
      ],
    ),
  );

  Widget _segmentsCard(AnalyticsInsights report) {
    final segments = report.segments.items;

    return ReportCard(
      title: 'Segmenti kupaca',
      subtitle: report.segments.source.caveat ??
          'K-Means grupisanje po ponašanju kupaca · ${report.segments.windowLabel}',
      child: segments.isEmpty
          ? _emptyBlock('Nema dovoljno kupaca za segmentaciju u ovom periodu.')
          : LayoutBuilder(
              builder: (context, constraints) {
                const gap = 16.0;
                final columns =
                    ReportSegmentCard.columnsFor(segments.length, constraints.maxWidth, gap: gap);
                final cardWidth = (constraints.maxWidth - gap * (columns - 1)) / columns;

                return Wrap(
                  spacing: gap,
                  runSpacing: gap,
                  children: [
                    for (var i = 0; i < segments.length; i++)
                      SizedBox(
                        width: cardWidth,
                        child: ReportSegmentCard(segment: segments[i], isLeading: i == 0),
                      ),
                  ],
                );
              },
            ),
    );
  }

  Widget _emptyBlock(String message) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 28),
    child: Center(
      child: Text(
        message,
        textAlign: TextAlign.center,
        style: TextStyle(
          fontSize: 13,
          color: AppColors.textTertiary(Theme.of(context).brightness),
        ),
      ),
    ),
  );

  // ── Shared bits ───────────────────────────────────────────────────────────

  /// The tile strip. Four across on a wide window, two on a medium one, one
  /// when the window is genuinely narrow — the same `LayoutBuilder` breakpoint
  /// approach `categories_screen.dart` uses.
  Widget _tileGrid(List<Widget> tiles, double width) {
    final columns = width >= 1100 ? 4 : (width >= 700 ? 2 : 1);
    const gap = 16.0;
    final tileWidth = (width - gap * (columns - 1)) / columns;

    return Wrap(
      spacing: gap,
      runSpacing: gap,
      children: [for (final tile in tiles) SizedBox(width: tileWidth, child: tile)],
    );
  }

  Color get _positive => _isDark ? AppColors.success : AppColors.successDark;

  Color _occupancyColor(double? percent) {
    if (percent == null) return AppColors.textTertiary(Theme.of(context).brightness);
    if (percent >= 80) return _positive;
    if (percent >= 50) return AppColors.accent;
    return AppColors.warningDark;
  }

  /// Mirrors `ReportPdfService.FileName`'s slugs.
  static String _tabSlug(ReportTab tab) => switch (tab) {
    ReportTab.sales => 'prodaja',
    ReportTab.products => 'proizvodi',
    ReportTab.redemption => 'iskoristenost',
    ReportTab.organizations => 'organizacije',
    ReportTab.insights => 'uvidi',
  };

  static String _isoDate(DateTime date) =>
      '${date.year.toString().padLeft(4, '0')}-'
      '${date.month.toString().padLeft(2, '0')}-'
      '${date.day.toString().padLeft(2, '0')}';

  /// `306k KM` — the chart's bars sit about 40px apart, which is nowhere near
  /// enough for a full `306.000,00 KM`.
  String _compactMoney(double amount) {
    if (amount >= 1000) return '${formatCount((amount / 1000).round())}k';
    return formatCount(amount.round());
  }
}
