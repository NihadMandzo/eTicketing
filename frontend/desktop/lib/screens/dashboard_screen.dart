import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/formatting.dart';
import '../models/responses/admin_user_response.dart';
import '../models/responses/category_response.dart';
import '../models/responses/organization_response.dart';
import '../models/responses/report_responses.dart';
import '../models/responses/user_profile.dart';
import '../models/search_objects/base_search_object.dart';
import '../models/search_objects/organization_user_search_object.dart';
import '../models/search_objects/product_search_object.dart';
import '../providers/category_provider.dart';
import '../providers/organization_provider.dart';
import '../providers/product_provider.dart';
import '../providers/report_provider.dart';
import '../providers/user_provider.dart';
import '../theme/app_colors.dart';
import 'widgets/reports/report_bar_chart.dart';
import 'widgets/reports/report_data_table.dart';
import 'widgets/reports/report_metric_card.dart';

/// Kontrolna tabla — the post-login landing page, built from
/// docs/Design/Dashboard.dc.html section-for-section, per role. Two things in
/// the design are deliberately not built, same "flag rather than fake" spirit
/// as reports_screen.dart's own three documented gaps:
///
/// - "Zahtijeva pažnju" (the attention/action queue) — out of scope by request.
/// - The yellow "Pregled kao:" role switcher — a mock-up affordance; the real
///   screen reads the role from the session, same precedent as Reports.
///
/// Two more sections the design shows have no real backing data anywhere in
/// this codebase (a platform-wide activity/audit log; a category-level sales
/// breakdown — Ticketing's schema has no Category reference at all) and are
/// left out entirely rather than invented. "Kanali prodaje" is built with the
/// two `TicketOrigin` values that actually exist (Online/Printed) rather than
/// the design's fictional third "Blagajna" channel.
class DashboardScreen extends StatefulWidget {
  final UserProfile user;

  const DashboardScreen({super.key, required this.user});

  @override
  State<DashboardScreen> createState() => _DashboardScreenState();
}

class _DashboardScreenState extends State<DashboardScreen> {
  bool _isLoading = true;

  int? _organizationsCount;
  int? _adminAccountsCount;
  int? _categoriesCount;
  int? _activeEventsCount;
  int? _draftsCount;
  int? _orgAdminsCount;

  SalesReport? _sales;
  ProductReport? _products;
  OrganizationReport? _organizations;
  List<UpcomingEventResponse>? _upcomingEvents;

  bool get _isSuperAdmin => widget.user.roleName == 'SuperAdmin';
  bool get _isAdmin => widget.user.roleName == 'Admin';
  bool get _isOrgSuperAdmin => widget.user.roleName == 'OrganizationSuperAdmin';
  bool get _isOrgAdmin => widget.user.roleName == 'OrganizationAdmin';
  bool get _isPlatformStaff => _isSuperAdmin || _isAdmin;
  bool get _isOrganizer => _isOrgSuperAdmin || _isOrgAdmin;

  /// SuperAdmin, OrganizationSuperAdmin and OrganizationAdmin keep the Sales
  /// report tab per `ReportService.Authorize` (reports_screen.dart's
  /// `_tabsByRole`) — Admin does not, so it gets no revenue card, no trend
  /// chart and no sales-channels card.
  bool get _hasSalesAccess => _isSuperAdmin || _isOrganizer;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _isLoading = true);

    final to = DateTime.now();
    final from = to.subtract(const Duration(days: 30));
    final probe = BaseSearchObject(page: 0, pageSize: 1);

    final futures = <Future<void>>[];

    if (_isPlatformStaff) {
      futures.add(_fetch(() async {
        final r = await OrganizationProvider().getAll(searchObject: probe, fromJson: OrganizationResponse.fromJson);
        _organizationsCount = r.totalCount;
      }));
    }

    if (_isSuperAdmin) {
      futures.add(_fetch(() async {
        final r = await AdminProvider().getAll(searchObject: probe, fromJson: AdminUserResponse.fromJson);
        _adminAccountsCount = r.totalCount;
      }));
    }

    if (_isAdmin) {
      futures.add(_fetch(() async {
        final r = await CategoryProvider().getAll(searchObject: probe, fromJson: CategoryResponse.fromJson);
        _categoriesCount = r.totalCount;
      }));
      futures.add(_fetch(() async {
        final r = await ProductProvider().getAllPlatform(searchObject: ProductSearchObject(page: 0, pageSize: 1, status: 1));
        _activeEventsCount = r.totalCount;
      }));
      futures.add(_fetch(() async {
        final r = await ProductProvider().getAllPlatform(searchObject: ProductSearchObject(page: 0, pageSize: 1, status: 0));
        _draftsCount = r.totalCount;
      }));
    }

    if (_isOrganizer) {
      futures.add(_fetch(() async {
        final r = await ProductProvider().getMine(searchObject: ProductSearchObject(page: 0, pageSize: 1, status: 1));
        _activeEventsCount = r.totalCount;
      }));
    }

    if (_isOrgSuperAdmin) {
      futures.add(_fetch(() async {
        final r = await OrganizationUsersProvider().getAll(
          organizationId: widget.user.organizationId!,
          searchObject: OrganizationUserSearchObject(page: 0, pageSize: 1, role: 'OrganizationAdmin'),
          fromJson: AdminUserResponse.fromJson,
        );
        _orgAdminsCount = r.totalCount;
      }));
    }

    if (_isOrgAdmin) {
      futures.add(_fetch(() async {
        final r = await ProductProvider().getMine(searchObject: ProductSearchObject(page: 0, pageSize: 1, status: 0));
        _draftsCount = r.totalCount;
      }));
    }

    if (_hasSalesAccess) {
      futures.add(_fetch(() async => _sales = await ReportProvider().getSales(from, to)));
    }

    if (_isPlatformStaff) {
      futures.add(_fetch(() async => _organizations = await ReportProvider().getOrganizations(from, to)));
    }

    // Available to every role that reaches this shell.
    futures.add(_fetch(() async => _products = await ReportProvider().getProducts(from, to)));
    futures.add(_fetch(() async => _upcomingEvents = await ReportProvider().getUpcomingEvents(count: 4)));

    await Future.wait(futures);
    if (!mounted) return;
    setState(() => _isLoading = false);
  }

  /// Runs one KPI fetch in isolation — a failure leaves its field `null` (the
  /// affected card/section then shows "—"/a fallback message) instead of
  /// taking the rest of the dashboard down with it.
  Future<void> _fetch(Future<void> Function() action) async {
    try {
      await action();
    } catch (_) {
      // Swallowed on purpose — see doc comment above.
    }
  }

  String get _subtitle => switch (widget.user.roleName) {
    'SuperAdmin' => 'Pregled cijele platforme — organizacije, korisnici i prihod',
    'Admin' => 'Operativni pregled platforme — organizacije i kategorije',
    'OrganizationSuperAdmin' => 'Pregled vaše organizacije — prihod, karte i osoblje',
    _ => 'Pregled vaše organizacije — prihod i aktivni događaji',
  };

  String _fmtInt(int? value) => value == null ? '—' : formatCount(value);

  Color _positive(Brightness brightness) =>
      brightness == Brightness.dark ? AppColors.success : AppColors.successDark;

  Color _accent(Brightness brightness) =>
      brightness == Brightness.dark ? AppColors.secondary : AppColors.primary;

  // ── Stat cards ───────────────────────────────────────────────────────────

  List<Widget> _kpiTiles() {
    final tiles = <Widget>[];

    if (_isSuperAdmin) {
      tiles.add(ReportMetricCard(label: 'Organizacije', value: _fmtInt(_organizationsCount), icon: LucideIcons.building2));
      tiles.add(ReportMetricCard(label: 'Administratora (platforma)', value: _fmtInt(_adminAccountsCount), icon: LucideIcons.users));
      tiles.add(_revenueTile('Ukupan prihod (30 dana)'));
      tiles.add(ReportMetricCard(
        label: 'Prodanih karata (30 dana)',
        value: _sales == null ? '—' : formatCount(_sales!.ticketsSold),
        icon: LucideIcons.ticket,
      ));
    } else if (_isAdmin) {
      tiles.add(ReportMetricCard(label: 'Organizacije', value: _fmtInt(_organizationsCount), icon: LucideIcons.building2));
      tiles.add(ReportMetricCard(label: 'Aktivni događaji', value: _fmtInt(_activeEventsCount), icon: LucideIcons.package));
      tiles.add(ReportMetricCard(label: 'Kategorije', value: _fmtInt(_categoriesCount), icon: LucideIcons.tags));
      tiles.add(ReportMetricCard(label: 'Nacrti (platforma)', value: _fmtInt(_draftsCount), icon: LucideIcons.filePenLine));
    } else if (_isOrgSuperAdmin) {
      tiles.add(_revenueTile('Prihod ove organizacije (30 dana)'));
      tiles.add(ReportMetricCard(
        label: 'Prodanih karata (30 dana)',
        value: _sales == null ? '—' : formatCount(_sales!.ticketsSold),
        icon: LucideIcons.ticket,
      ));
      tiles.add(ReportMetricCard(label: 'Aktivni događaji', value: _fmtInt(_activeEventsCount), icon: LucideIcons.package));
      tiles.add(ReportMetricCard(label: 'Administratora', value: _fmtInt(_orgAdminsCount), icon: LucideIcons.userCog));
    } else if (_isOrgAdmin) {
      tiles.add(_revenueTile('Prihod ove organizacije (30 dana)'));
      tiles.add(ReportMetricCard(
        label: 'Prodanih karata (30 dana)',
        value: _sales == null ? '—' : formatCount(_sales!.ticketsSold),
        icon: LucideIcons.ticket,
      ));
      tiles.add(ReportMetricCard(label: 'Aktivni događaji', value: _fmtInt(_activeEventsCount), icon: LucideIcons.package));
      tiles.add(ReportMetricCard(label: 'Nacrti', value: _fmtInt(_draftsCount), icon: LucideIcons.filePenLine));
    }

    return tiles;
  }

  /// The one card with a trend badge — every other stat card is a plain
  /// count, and there is no "N days ago" comparison for any of them; revenue
  /// is the only figure the backend already computes period-over-period.
  Widget _revenueTile(String label) {
    final sales = _sales;
    return ReportMetricCard(
      label: label,
      value: sales == null ? '—' : formatMoney(sales.grossRevenue),
      hint: sales?.revenueChangePercent == null
          ? null
          : '${formatSignedPercent(sales!.revenueChangePercent)} u odnosu na prethodni period',
      emphasis: sales?.revenueChangePercent == null
          ? ReportEmphasis.neutral
          : (sales!.revenueChangePercent! >= 0 ? ReportEmphasis.positive : ReportEmphasis.negative),
      icon: LucideIcons.banknote,
    );
  }

  /// Same breakpoint math as `reports_screen.dart`'s private `_tileGrid`.
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

  /// One column of full-width cards, with a gap *between* cards only. The trailing gap the
  /// previous spread emitted after the last card added 20px of dead space to the bottom of every
  /// column and to the page as a whole.
  static Widget _cardColumn(List<Widget> cards) => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          for (var i = 0; i < cards.length; i++) ...[
            if (i > 0) const SizedBox(height: 20),
            cards[i],
          ],
        ],
      );

  // ── Sales trend ──────────────────────────────────────────────────────────

  Widget _trendCard(Brightness brightness) {
    final sales = _sales;
    if (sales == null) {
      return ReportCard(
        title: 'Prihod',
        child: Text('Podaci nisu dostupni.', style: TextStyle(fontSize: 13, color: AppColors.textTertiary(brightness))),
      );
    }

    final peak = sales.buckets.isEmpty ? 0.0 : sales.buckets.map((b) => b.revenue).reduce((a, b) => a > b ? a : b);
    final days = sales.period.days == 0 ? 1 : sales.period.days;
    final perDay = (sales.ticketsSold / days).round();

    return ReportCard(
      title: 'Prihod ${sales.period.bucketUnit.title}',
      subtitle: 'Posljednjih 30 dana · prosječno ${formatCount(perDay)} ${perDay == 1 ? 'karta' : 'karata'} dnevno',
      child: ReportBarChart(
        bars: [
          for (final bucket in sales.buckets)
            ReportBarData(
              label: bucket.label,
              value: _compactMoney(bucket.revenue),
              ratio: peak == 0 ? 0 : bucket.revenue / peak,
            ),
        ],
      ),
    );
  }

  String _compactMoney(double amount) {
    if (amount >= 1000) return '${formatCount((amount / 1000).round())}k';
    return formatCount(amount.round());
  }

  // ── Upcoming events ──────────────────────────────────────────────────────

  Widget _upcomingEventsCard(Brightness brightness) {
    final events = _upcomingEvents;
    return ReportCard(
      title: _isPlatformStaff ? 'Nadolazeći događaji (svi)' : 'Nadolazeći događaji',
      child: events == null || events.isEmpty
          ? Padding(
              padding: const EdgeInsets.symmetric(vertical: 12),
              child: Text('Nema nadolazećih događaja.',
                  style: TextStyle(fontSize: 13, color: AppColors.textTertiary(brightness))),
            )
          : Column(
              children: [
                for (final event in events) _UpcomingEventTile(event: event, isLast: event == events.last),
              ],
            ),
    );
  }

  // ── Top selling products ─────────────────────────────────────────────────

  Widget _topSellingCard(Brightness brightness) {
    final rows = _products?.rows.take(3).toList() ?? const <ProductReportRow>[];
    return ReportCard(
      title: 'Najprodavaniji proizvodi',
      child: rows.isEmpty
          ? Padding(
              padding: const EdgeInsets.symmetric(vertical: 12),
              child: Text('Nema podataka za odabrani period.',
                  style: TextStyle(fontSize: 13, color: AppColors.textTertiary(brightness))),
            )
          : Column(
              children: [
                for (var i = 0; i < rows.length; i++)
                  _TopSellingTile(rank: i + 1, row: rows[i], isLast: i == rows.length - 1, positive: _positive(brightness)),
              ],
            ),
    );
  }

  // ── Sales channels ───────────────────────────────────────────────────────

  Widget _salesChannelsCard(Brightness brightness) {
    final sales = _sales;
    if (sales == null) {
      return ReportCard(
        title: 'Kanali prodaje',
        subtitle: 'Posljednjih 30 dana',
        child: Text('Podaci nisu dostupni.', style: TextStyle(fontSize: 13, color: AppColors.textTertiary(brightness))),
      );
    }

    final total = sales.onlineSold + sales.printedSold;
    return ReportCard(
      title: 'Kanali prodaje',
      subtitle: 'Posljednjih 30 dana',
      child: Column(
        children: [
          _ChannelBar(
            icon: LucideIcons.smartphone,
            label: 'Online (mobilna app)',
            count: sales.onlineSold,
            total: total,
            color: _accent(brightness),
          ),
          const SizedBox(height: 16),
          _ChannelBar(
            icon: LucideIcons.printer,
            label: 'Štampane ulaznice',
            count: sales.printedSold,
            total: total,
            color: AppColors.warningDark,
          ),
        ],
      ),
    );
  }

  // ── Organization overview (platform roles only) ─────────────────────────

  Widget _orgOverviewCard(Brightness brightness) {
    final report = _organizations;
    if (report == null) {
      return ReportCard(
        title: 'Pregled organizacija',
        child: Text('Podaci nisu dostupni.', style: TextStyle(fontSize: 13, color: AppColors.textTertiary(brightness))),
      );
    }

    final financial = report.view == OrganizationReportView.financial;
    final rows = report.rows.take(4).toList();
    final positive = _positive(brightness);

    return ReportCard(
      title: 'Pregled organizacija',
      child: ReportDataTable(
        columns: financial
            ? const [
                ReportColumn('Organizacija', flex: 26),
                ReportColumn('Karte', flex: 12, rightAligned: true),
                ReportColumn('Prihod', flex: 14, rightAligned: true),
              ]
            : const [
                ReportColumn('Organizacija', flex: 26),
                ReportColumn('Proizvodi', flex: 12, rightAligned: true),
                ReportColumn('Status', flex: 14),
              ],
        rows: [
          for (final row in rows)
            financial
                ? [
                    ReportCell.custom(ReportNameCell(name: row.name, meta: row.address)),
                    ReportCell(formatCount(row.tickets ?? 0)),
                    ReportCell(formatMoney(row.revenue ?? 0), color: positive, bold: true),
                  ]
                : [
                    ReportCell.custom(ReportNameCell(name: row.name, meta: row.address)),
                    ReportCell(formatCount(row.products)),
                    ReportCell.custom(ReportBadge(
                      label: row.isPending == true ? 'Na čekanju' : 'Aktivna',
                      color: row.isPending == true ? AppColors.warningDark : positive,
                    )),
                  ],
        ],
      ),
    );
  }

  // ── Build ─────────────────────────────────────────────────────────────────

  @override
  Widget build(BuildContext context) {
    if (_isLoading) {
      return const Center(child: CircularProgressIndicator());
    }

    final brightness = Theme.of(context).brightness;
    final isDark = brightness == Brightness.dark;
    final tiles = _kpiTiles();

    // Built once, then arranged differently per breakpoint below.
    final upcoming = _upcomingEventsCard(brightness);
    final topSelling = _topSellingCard(brightness);
    final orgOverview = _isPlatformStaff ? _orgOverviewCard(brightness) : null;
    final salesChannels = _hasSalesAccess ? _salesChannelsCard(brightness) : null;

    // Narrow layout: one column, in reading order.
    final stacked = <Widget>[upcoming, ?orgOverview, topSelling, ?salesChannels];

    // Wide layout: two balanced columns. Cards are assigned by hand to keep the two columns
    // ending at roughly the same height — the tall events card alone on the left, the shorter
    // cards paired on the right — instead of the old 3-vs-1 split that left the bottom-right
    // corner empty. Every role now populates both columns, so a platform Admin gets a right
    // rail too (it previously had none, which forced them into the single-column branch).
    //
    // Balanced by construction rather than measured: ReportDataTable builds inside a
    // LayoutBuilder, which does not support intrinsic sizing, so IntrinsicHeight cannot be used
    // to equalise the two columns exactly.
    final left = <Widget>[
      upcoming,
      if (_isPlatformStaff && salesChannels != null) salesChannels,
    ];
    final right = <Widget>[
      ?orgOverview,
      topSelling,
      if (!_isPlatformStaff && salesChannels != null) salesChannels,
    ];

    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Kontrolna tabla',
            style: TextStyle(
              fontSize: 24,
              fontWeight: FontWeight.w700,
              color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
              letterSpacing: -0.4,
            ),
          ),
          const SizedBox(height: 8),
          Text(_subtitle, style: TextStyle(fontSize: 14, color: AppColors.textTertiary(brightness))),
          const SizedBox(height: 24),
          if (tiles.isNotEmpty)
            LayoutBuilder(builder: (context, constraints) => _tileGrid(tiles, constraints.maxWidth)),
          if (_hasSalesAccess) ...[
            const SizedBox(height: 24),
            _trendCard(brightness),
          ],
          const SizedBox(height: 24),
          LayoutBuilder(
            builder: (context, constraints) {
              if (constraints.maxWidth < 1100 || right.isEmpty) {
                return _cardColumn(stacked);
              }

              // Equal flex, not 16/10: two columns carrying comparable content should be the
              // same width, or the narrower one's cards grow taller and reopen the gap.
              return Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(child: _cardColumn(left)),
                  const SizedBox(width: 20),
                  Expanded(child: _cardColumn(right)),
                ],
              );
            },
          ),
        ],
      ),
    );
  }
}

// ─── Row widgets ────────────────────────────────────────────────────────────

class _UpcomingEventTile extends StatelessWidget {
  final UpcomingEventResponse event;
  final bool isLast;

  const _UpcomingEventTile({required this.event, required this.isLast});

  @override
  Widget build(BuildContext context) {
    final brightness = Theme.of(context).brightness;
    final isDark = brightness == Brightness.dark;
    final accent = isDark ? AppColors.secondary : AppColors.primary;
    final pct = event.capacity == 0 ? null : (event.sold / event.capacity * 100).clamp(0, 100);
    final barColor = pct == null
        ? AppColors.textTertiary(brightness)
        : (pct >= 80
            ? (isDark ? AppColors.success : AppColors.successDark)
            : (pct >= 40 ? accent : AppColors.warningDark));

    return Container(
      padding: const EdgeInsets.symmetric(vertical: 12),
      decoration: BoxDecoration(
        border: Border(
          bottom: isLast
              ? BorderSide.none
              : BorderSide(color: isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted),
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          Container(
            width: 42,
            height: 42,
            decoration: BoxDecoration(color: accent.withValues(alpha: 0.1), borderRadius: BorderRadius.circular(10)),
            child: Icon(LucideIcons.calendarDays, size: 19, color: accent),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  event.name,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.textPrimary(brightness)),
                ),
                const SizedBox(height: 2),
                Text(
                  event.meta,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(fontSize: 12, color: AppColors.textTertiary(brightness)),
                ),
                const SizedBox(height: 7),
                Row(
                  children: [
                    Expanded(
                      child: ConstrainedBox(
                        constraints: const BoxConstraints(maxWidth: 220),
                        child: Container(
                          height: 6,
                          decoration: BoxDecoration(
                            color: isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted,
                            borderRadius: BorderRadius.circular(3),
                          ),
                          child: pct == null
                              ? null
                              : FractionallySizedBox(
                                  alignment: Alignment.centerLeft,
                                  widthFactor: (pct / 100).clamp(0.0, 1.0),
                                  child: Container(
                                    decoration: BoxDecoration(color: barColor, borderRadius: BorderRadius.circular(3)),
                                  ),
                                ),
                        ),
                      ),
                    ),
                    const SizedBox(width: 8),
                    Text(
                      pct == null
                          ? '${formatCount(event.sold)} prodano'
                          : '${pct.round()}% · ${formatCount(event.sold)}/${formatCount(event.capacity)}',
                      style: TextStyle(fontSize: 11, color: AppColors.textTertiary(brightness)),
                    ),
                  ],
                ),
              ],
            ),
          ),
          const SizedBox(width: 10),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
            decoration: BoxDecoration(color: accent.withValues(alpha: 0.08), borderRadius: BorderRadius.circular(20)),
            child: Text(
              formatDate(event.date),
              style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: accent),
            ),
          ),
        ],
      ),
    );
  }
}

class _TopSellingTile extends StatelessWidget {
  final int rank;
  final ProductReportRow row;
  final bool isLast;
  final Color positive;

  const _TopSellingTile({required this.rank, required this.row, required this.isLast, required this.positive});

  @override
  Widget build(BuildContext context) {
    final brightness = Theme.of(context).brightness;
    final isDark = brightness == Brightness.dark;

    return Container(
      padding: const EdgeInsets.symmetric(vertical: 11),
      decoration: BoxDecoration(
        border: Border(
          bottom: isLast
              ? BorderSide.none
              : BorderSide(color: isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted),
        ),
      ),
      child: Row(
        children: [
          Container(
            width: 26,
            height: 26,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted,
              borderRadius: BorderRadius.circular(8),
            ),
            child: Text(
              '$rank',
              style: TextStyle(fontSize: 12, fontWeight: FontWeight.w700, color: AppColors.textSecondary(brightness)),
            ),
          ),
          const SizedBox(width: 12),
          Expanded(child: ReportNameCell(name: row.name, meta: '${formatCount(row.sold)} karata')),
          const SizedBox(width: 8),
          Text(
            formatMoney(row.revenue),
            style: TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: positive),
          ),
        ],
      ),
    );
  }
}

class _ChannelBar extends StatelessWidget {
  final IconData icon;
  final String label;
  final int count;
  final int total;
  final Color color;

  const _ChannelBar({required this.icon, required this.label, required this.count, required this.total, required this.color});

  @override
  Widget build(BuildContext context) {
    final brightness = Theme.of(context).brightness;
    final isDark = brightness == Brightness.dark;
    final pct = total == 0 ? 0.0 : count / total * 100;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(icon, size: 14, color: color),
                const SizedBox(width: 7),
                Flexible(
                  child: Text(
                    label,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(fontSize: 13, fontWeight: FontWeight.w500, color: AppColors.textSecondary(brightness)),
                  ),
                ),
              ],
            ),
            Text(
              '${formatCount(count)} · ${pct.round()}%',
              style: TextStyle(fontSize: 12, color: AppColors.textTertiary(brightness)),
            ),
          ],
        ),
        const SizedBox(height: 6),
        Container(
          height: 8,
          decoration: BoxDecoration(
            color: isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted,
            borderRadius: BorderRadius.circular(4),
          ),
          child: FractionallySizedBox(
            alignment: Alignment.centerLeft,
            widthFactor: (pct / 100).clamp(0.0, 1.0),
            child: Container(decoration: BoxDecoration(color: color, borderRadius: BorderRadius.circular(4))),
          ),
        ),
      ],
    );
  }
}
