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
import 'widgets/reports/report_metric_card.dart';
import 'widgets/reports/report_range_bar.dart';

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

  /// The tabs each role may see — the client half of the matrix in
  /// `ReportService.Authorize`.
  static const _tabsByRole = <String, List<ReportTab>>{
    'SuperAdmin': [ReportTab.sales, ReportTab.products, ReportTab.redemption, ReportTab.organizations],
    'Admin': [ReportTab.products, ReportTab.organizations],
    'OrganizationSuperAdmin': [ReportTab.sales, ReportTab.products, ReportTab.redemption],
    'OrganizationAdmin': [ReportTab.sales, ReportTab.products],
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
            trailing: _canExport ? _exportButton() : null,
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

    final breakdown = ReportCard(
      title: 'Razrada prihoda',
      child: Column(
        children: [
          _BreakdownRow(
            label: 'Bruto prodaja karata',
            share: '100%',
            value: formatMoney(report.grossRevenue),
          ),
          _BreakdownRow(
            label: 'Otkazane karte',
            share: formatPercent(report.cancellationRatePercent),
            value: '−${formatMoney(report.cancelledAmount)}',
            emphasis: ReportEmphasis.negative,
          ),
          _BreakdownRow(
            label: 'Neto prihod',
            value: formatMoney(report.netRevenue),
            emphasis: ReportEmphasis.positive,
            isLast: true,
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
      ReportMetricCard(
        label: 'Otkazane karte',
        value: formatCount(report.cancelledCount),
        hint: 'Stopa otkaza ${formatPercent(report.cancellationRatePercent)}',
        emphasis: ReportEmphasis.negative,
      ),
    ];

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
              const SizedBox(height: 16),
              breakdown,
            ],
          );
        }

        return Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              flex: 16,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [chart, const SizedBox(height: 16), breakdown],
              ),
            ),
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
        );
      },
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
                ReportColumn('Proizvod', flex: 26),
                ReportColumn('Prodano', flex: 9, rightAligned: true),
                ReportColumn('Popunjenost', flex: 13),
                ReportColumn('Pros. cijena', flex: 12, rightAligned: true),
                ReportColumn('Otkazano', flex: 9, rightAligned: true),
                ReportColumn('Prihod', flex: 13, rightAligned: true),
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
                    ReportCell(formatCount(row.cancelled), color: row.cancelled > 0 ? AppColors.error : null),
                    ReportCell(formatMoney(row.revenue), color: _positive, bold: true),
                  ],
              ],
              totalsRow: [
                const ReportCell('Ukupno'),
                ReportCell(formatCount(report.totalSold)),
                ReportCell('${formatPercent(report.averageOccupancyPercent)} pros.'),
                ReportCell(formatMoney(report.averagePrice)),
                ReportCell(formatCount(report.totalCancelled)),
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

class _BreakdownRow extends StatelessWidget {
  final String label;
  final String? share;
  final String value;
  final ReportEmphasis emphasis;
  final bool isLast;

  const _BreakdownRow({
    required this.label,
    this.share,
    required this.value,
    this.emphasis = ReportEmphasis.neutral,
    this.isLast = false,
  });

  @override
  Widget build(BuildContext context) {
    final brightness = Theme.of(context).brightness;
    final isDark = brightness == Brightness.dark;

    return Container(
      padding: const EdgeInsets.symmetric(vertical: 9),
      decoration: BoxDecoration(
        border: Border(
          bottom: isLast
              ? BorderSide.none
              : BorderSide(color: isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted),
        ),
      ),
      child: Row(
        children: [
          Expanded(
            child: Text(label, style: TextStyle(fontSize: 13, color: AppColors.textSecondary(brightness))),
          ),
          if (share != null) ...[
            Text(share!, style: TextStyle(fontSize: 12, color: AppColors.textTertiary(brightness))),
            const SizedBox(width: 12),
          ],
          Text(
            value,
            style: TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w700,
              color: ReportMetricCard.colorFor(emphasis, brightness),
            ),
          ),
        ],
      ),
    );
  }
}
