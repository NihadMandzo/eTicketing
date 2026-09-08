import 'dart:async';

import 'package:flutter/material.dart';

import '../models/requests/hold_sector_request.dart';
import '../models/responses/organization_response.dart';
import '../models/responses/product_response.dart';
import '../models/responses/sector_response.dart';
import '../services/api_exception.dart';
import '../services/cart.dart';
import '../services/catalog_service.dart';
import '../services/organization_service.dart';
import '../services/sector_service.dart';
import '../theme/app_colors.dart';
import '../widgets/product_location_map.dart';
import '../widgets/purchase_widgets.dart';
import '../widgets/responsive_page.dart';
import 'login_screen.dart';
import 'payment_screen.dart';

class _TicketTypeRow {
  final SectorResponse sector;
  final String? ticketTypeId;
  final String? ticketTypeName;
  final double price;

  const _TicketTypeRow({
    required this.sector,
    this.ticketTypeId,
    this.ticketTypeName,
    required this.price,
  });

  String get key => '${sector.id}::${ticketTypeId ?? 'flat'}';
}

/// DailyEntry product details (mockup screen 9) — hero, an inline
/// month-calendar grid to pick the visit date, then per-TicketType qty rows
/// against the one Sector's shared per-day capacity.
class MuseumTicketScreen extends StatefulWidget {
  final String productId;

  const MuseumTicketScreen({super.key, required this.productId});

  @override
  State<MuseumTicketScreen> createState() => _MuseumTicketScreenState();
}

class _MuseumTicketScreenState extends State<MuseumTicketScreen> {
  final _catalogService = CatalogService();
  final _sectorService = SectorService();
  final _organizationService = OrganizationService();

  ProductResponse? _product;
  List<SectorResponse> _sectors = [];
  OrganizationResponse? _organization;
  bool _isLoading = true;
  String? _loadError;

  late DateTime _visibleMonth;
  DateTime? _selectedDate;
  final Map<String, int> _quantities = {};
  bool _isCheckingAvailability = false;
  bool _isSubmitting = false;
  String? _submitError;

  @override
  void initState() {
    super.initState();
    // Provisional until the sectors load and tell us which months are
    // actually bookable (see _firstSelectableDate).
    final first = _tomorrow();
    _visibleMonth = DateTime(first.year, first.month);
    _selectedDate = first;
    _load();
  }

  static DateTime _tomorrow() {
    final now = DateTime.now().add(const Duration(days: 1));
    return DateTime(now.year, now.month, now.day);
  }

  /// Earliest bookable day: tomorrow if that already falls inside a sector's
  /// period, otherwise day 1 of the earliest period still ahead of us. A
  /// DailyEntry sector covers exactly one calendar month
  /// (PeriodYear/PeriodMonth), so opening the calendar on the current month
  /// showed an empty ticket list for any museum whose sectors start later.
  static DateTime _firstSelectableDate(List<SectorResponse> sectors) {
    final earliest = _tomorrow();
    final periods =
        sectors
            .where((s) => s.periodYear != null && s.periodMonth != null)
            .map((s) => DateTime(s.periodYear!, s.periodMonth!))
            .toList()
          ..sort();

    for (final period in periods) {
      final lastDay = DateTime(period.year, period.month + 1, 0);
      if (lastDay.isBefore(earliest)) continue;
      return period.isAfter(earliest) ? period : earliest;
    }
    return earliest;
  }

  Future<void> _load() async {
    try {
      final product = await _catalogService.getProductById(widget.productId);
      // Dateless on purpose: which day is even selectable is derived from the sectors' periods, so
      // there is no date to ask availability for yet. _refreshAvailability below immediately asks
      // again for the day this picks, which is the one the buyer is looking at.
      final sectors = await _sectorService.getSectors(widget.productId);
      OrganizationResponse? org;
      try {
        org = await _organizationService.getById(product.organizationId);
      } catch (_) {}
      if (!mounted) return;
      final firstDate = _firstSelectableDate(sectors.items);
      setState(() {
        _product = product;
        _sectors = sectors.items;
        _organization = org;
        _selectedDate = firstDate;
        _visibleMonth = DateTime(firstDate.year, firstDate.month);
        _isLoading = false;
      });
      unawaited(_refreshAvailability(firstDate));
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _loadError = 'Događaj nije pronađen ili više nije dostupan.';
        _isLoading = false;
      });
    }
  }

  /// Only the sectors covering the selected date's month — the backend
  /// rejects any other hold with `sector.date_out_of_period` (see
  /// SectorService.HoldAsync). Flattening every period's sectors into one
  /// list, as this used to, offered a museum's September *and* October
  /// tickets at once and then failed the hold for whichever didn't match.
  List<SectorResponse> get _sectorsForSelectedDate {
    final date = _selectedDate;
    if (date == null) return const [];
    return _sectors
        .where((s) => s.periodYear == date.year && s.periodMonth == date.month)
        .toList();
  }

  List<_TicketTypeRow> get _rows => _sectorsForSelectedDate.expand((sector) {
    if (sector.ticketTypes.isNotEmpty) {
      return sector.ticketTypes.map(
        (tt) => _TicketTypeRow(
          sector: sector,
          ticketTypeId: tt.id,
          ticketTypeName: tt.name,
          price: tt.price,
        ),
      );
    }
    return [_TicketTypeRow(sector: sector, price: sector.price)];
  }).toList();

  /// Per-order ceiling, unchanged from before availability existed.
  static const _maxPerOrder = 10;

  /// Every sector covering the selected day is exhausted — the museum is full that day, which is
  /// a different message from "this museum sells nothing".
  bool get _isSelectedDateSoldOut =>
      _sectorsForSelectedDate.isNotEmpty && _sectorsForSelectedDate.every((s) => s.isSoldOut);

  /// How many more admissions this sector can take on the selected day, after the rest of the
  /// selection. Ticket types share one per-day pool, so the budget is counted per sector, not per
  /// row. Null capacity means the backend didn't state one, and an unknown must not cap anything.
  int _remainingForSector(SectorResponse sector) {
    final capacity = sector.remainingCapacity;
    if (capacity == null) return _maxPerOrder;

    final selected = _rows
        .where((row) => row.sector.id == sector.id)
        .fold(0, (sum, row) => sum + (_quantities[row.key] ?? 0));
    return (capacity - selected).clamp(0, _maxPerOrder);
  }

  /// Trims any quantity that no longer fits after `_sectors` picks up a new day's real capacity.
  /// `_quantities` is keyed date-independently (`sector.id::ticketTypeId`), so switching to a
  /// different day within the same month otherwise leaves a quantity chosen against the old day's
  /// capacity in place against the new one, silently exceeding it. Ticket types share one per-day
  /// pool per sector, so this walks each sector's rows in order and trims from the tail once the
  /// running total exceeds what's left — clearing every selection instead would also discard picks
  /// on sectors the date switch didn't affect.
  void _clampQuantitiesToCapacity() {
    for (final sector in _sectors) {
      final capacity = sector.remainingCapacity;
      if (capacity == null) continue; // unknown must not cap anything, see _remainingForSector

      var budget = capacity;
      for (final row in _rows.where((r) => r.sector.id == sector.id)) {
        final selected = _quantities[row.key] ?? 0;
        if (selected == 0) continue;
        final allowed = selected.clamp(0, budget);
        if (allowed != selected) _quantities[row.key] = allowed;
        budget -= allowed;
      }
    }
  }

  double get _total => _rows.fold(
    0,
    (sum, row) => sum + (_quantities[row.key] ?? 0) * row.price,
  );

  bool get _hasSelection => _quantities.values.any((q) => q > 0);

  void _changeMonth(int delta) {
    setState(() {
      _visibleMonth = DateTime(_visibleMonth.year, _visibleMonth.month + delta);
      _selectedDate = null;
      _quantities.clear();
      _submitError = null;
    });
  }

  void _selectDate(DateTime date) {
    setState(() {
      final changedMonth =
          _selectedDate == null ||
          _selectedDate!.month != date.month ||
          _selectedDate!.year != date.year;
      _selectedDate = date;
      if (changedMonth) {
        _quantities.clear();
        _submitError = null;
      }
    });
    // A DailyEntry sector's capacity is counted per (Sector, date), so every day has its own
    // answer — the 14th can be full while the 15th is wide open. Availability is re-read on every
    // pick rather than once per month for exactly that reason.
    unawaited(_refreshAvailability(date));
  }

  /// Re-reads the sectors for [date] so `remainingCapacity` describes the day on screen.
  ///
  /// Failure leaves the previous numbers in place instead of surfacing an error: the hold endpoint
  /// is still the authority on capacity, so the worst case is a buyer picking a quantity and being
  /// told at hold time — exactly the behaviour before availability existed.
  Future<void> _refreshAvailability(DateTime date) async {
    setState(() => _isCheckingAvailability = true);
    try {
      final sectors = await _sectorService.getSectors(widget.productId, date: date);
      if (!mounted) return;
      // Ignore a response that landed after the buyer moved on to a different day.
      final current = _selectedDate;
      if (current == null || current.year != date.year || current.month != date.month || current.day != date.day) {
        return;
      }
      setState(() {
        _sectors = sectors.items;
        _clampQuantitiesToCapacity();
      });
    } catch (_) {
      // Deliberately silent — see above.
    } finally {
      if (mounted) setState(() => _isCheckingAvailability = false);
    }
  }

  Future<void> _proceedToCheckout() async {
    final product = _product;
    final date = _selectedDate;
    if (product == null || date == null || _isSubmitting) return;

    final bySector = <String, List<_TicketTypeRow>>{};
    for (final row in _rows) {
      if ((_quantities[row.key] ?? 0) > 0) {
        bySector.putIfAbsent(row.sector.id, () => []).add(row);
      }
    }
    if (bySector.isEmpty) return;

    setState(() {
      _isSubmitting = true;
      _submitError = null;
    });

    final dateStr =
        '${date.year.toString().padLeft(4, '0')}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';

    // Declared outside the try so both catch clauses below can see (and release) whichever holds
    // already succeeded before a later Sector's hold call failed — otherwise those holds just sit
    // there ticking down their own TTL instead of being released immediately.
    final holds = <CartHoldGroup>[];
    try {
      for (final entry in bySector.entries) {
        final quantity = entry.value.fold(
          0,
          (sum, row) => sum + (_quantities[row.key] ?? 0),
        );
        final hold = await _sectorService.hold(
          entry.key,
          HoldSectorRequest(quantity: quantity, date: dateStr),
        );
        holds.add(
          CartHoldGroup(
            holdId: hold.holdId,
            sectorId: entry.key,
            sectorName: entry.value.first.sector.name,
            lineItems: entry.value
                .map(
                  (row) => CartLineItem(
                    ticketTypeId: row.ticketTypeId,
                    ticketTypeName: row.ticketTypeName,
                    quantity: _quantities[row.key] ?? 0,
                    unitPrice: row.price,
                  ),
                )
                .toList(),
          ),
        );
      }

      Cart.set(
        CartState(
          productId: product.id,
          productName: product.name,
          eventSummary: _formatDate(date),
          date: dateStr,
          holds: holds,
        ),
      );

      if (!mounted) return;
      Navigator.of(
        context,
      ).push(MaterialPageRoute(builder: (_) => const PaymentScreen()));
    } on ApiException catch (e) {
      for (final hold in holds) {
        unawaited(_sectorService.release(hold.holdId));
      }
      if (!mounted) return;
      if (e.statusCode == 401) {
        Navigator.of(context).pushAndRemoveUntil(
          MaterialPageRoute(builder: (_) => const LoginScreen()),
          (route) => false,
        );
        return;
      }
      setState(() => _submitError = e.apiError.displayMessage);
    } catch (_) {
      for (final hold in holds) {
        unawaited(_sectorService.release(hold.holdId));
      }
      if (mounted) {
        setState(() => _submitError = 'Došlo je do greške. Pokušajte ponovo.');
      }
    } finally {
      if (mounted) setState(() => _isSubmitting = false);
    }
  }

  static const _months = [
    'Januar',
    'Februar',
    'Mart',
    'April',
    'Maj',
    'Juni',
    'Juli',
    'August',
    'Septembar',
    'Oktobar',
    'Novembar',
    'Decembar',
  ];
  static const _weekdayLabels = ['P', 'U', 'S', 'Č', 'P', 'S', 'N'];

  static String _formatDate(DateTime date) =>
      '${date.day}. ${_months[date.month - 1]} ${date.year}.';

  @override
  Widget build(BuildContext context) {
    if (_isLoading) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }
    if (_loadError != null || _product == null) {
      return Scaffold(
        appBar: AppBar(title: const Text('Muzejska ulaznica')),
        body: Center(child: Text(_loadError ?? 'Greška')),
      );
    }

    final product = _product!;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark
        ? AppColors.darkTextTertiary
        : AppColors.lightTextTertiary;
    final tomorrow = DateTime.now().add(const Duration(days: 1));
    final firstSelectableDay = DateTime(
      tomorrow.year,
      tomorrow.month,
      tomorrow.day,
    );

    return Scaffold(
      body: Stack(
        children: [
          SafeArea(
            bottom: false,
            child: SingleChildScrollView(
              padding: const EdgeInsets.only(bottom: 100),
              child: ResponsivePage(
                padding: EdgeInsets.zero,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Stack(
                      children: [
                        Container(
                          height: 200,
                          width: double.infinity,
                          decoration: const BoxDecoration(
                            gradient: LinearGradient(
                              colors: [AppColors.primary, AppColors.secondary],
                              begin: Alignment.topLeft,
                              end: Alignment.bottomRight,
                            ),
                          ),
                        ),
                        Positioned(
                          top: 0,
                          left: 0,
                          child: CircleBackButton(
                            onTap: () => Navigator.of(context).pop(),
                          ),
                        ),
                      ],
                    ),
                    Padding(
                      padding: const EdgeInsets.fromLTRB(20, 20, 20, 0),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              Container(
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 10,
                                  vertical: 4,
                                ),
                                decoration: BoxDecoration(
                                  color: AppColors.accent,
                                  borderRadius: BorderRadius.circular(6),
                                ),
                                child: Text(
                                  product.categoryName,
                                  style: const TextStyle(
                                    color: Colors.white,
                                    fontSize: 11,
                                    fontWeight: FontWeight.w700,
                                  ),
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: 12),
                          Text(
                            product.name,
                            style: const TextStyle(
                              fontSize: 22,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          const SizedBox(height: 20),
                          ProductLocationMap(
                            latitude: product.latitude,
                            longitude: product.longitude,
                            city: product.city,
                          ),
                          if (_organization != null) ...[
                            const SizedBox(height: 20),
                            const Text(
                              'Organizator',
                              style: TextStyle(
                                fontSize: 15,
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                            const SizedBox(height: 10),
                            OrganizerCard(organization: _organization!),
                          ],
                          const SizedBox(height: 20),
                          const Text(
                            'Odaberite datum posjete',
                            style: TextStyle(
                              fontSize: 15,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          const SizedBox(height: 12),
                          Row(
                            mainAxisAlignment: MainAxisAlignment.spaceBetween,
                            children: [
                              IconButton(
                                icon: const Icon(Icons.chevron_left_rounded),
                                onPressed: () => _changeMonth(-1),
                              ),
                              Text(
                                '${_months[_visibleMonth.month - 1]} ${_visibleMonth.year}',
                                style: const TextStyle(
                                  fontSize: 14,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                              IconButton(
                                icon: const Icon(Icons.chevron_right_rounded),
                                onPressed: () => _changeMonth(1),
                              ),
                            ],
                          ),
                          _CalendarGrid(
                            visibleMonth: _visibleMonth,
                            firstSelectableDay: firstSelectableDay,
                            selectedDate: _selectedDate,
                            onSelect: _selectDate,
                          ),
                          const SizedBox(height: 12),
                          const Text(
                            'Vrsta ulaznice',
                            style: TextStyle(
                              fontSize: 15,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          const SizedBox(height: 12),
                          if (_isSelectedDateSoldOut) ...[
                            Container(
                              width: double.infinity,
                              padding: const EdgeInsets.all(14),
                              decoration: BoxDecoration(
                                color: isDark
                                    ? AppColors.darkSurfaceMuted
                                    : AppColors.lightSurfaceMuted,
                                borderRadius: BorderRadius.circular(12),
                              ),
                              child: Text(
                                'Za ovaj datum je sve rasprodano. Odaberite drugi dan.',
                                style: TextStyle(
                                  fontSize: 13,
                                  fontWeight: FontWeight.w600,
                                  color: tertiaryText,
                                ),
                              ),
                            ),
                            const SizedBox(height: 14),
                          ],
                          if (_rows.isEmpty)
                            Text(
                              _sectors.isEmpty
                                  ? 'Nema dostupnih ulaznica za ovaj muzej.'
                                  : 'Za odabrani datum nema dostupnih ulaznica. Odaberite drugi datum.',
                              style: TextStyle(color: tertiaryText),
                            )
                          else
                            Column(
                              children: [
                                for (final row in _rows)
                                  Padding(
                                    padding: const EdgeInsets.only(bottom: 14),
                                    child: Builder(
                                      builder: (context) {
                                        final quantity = _quantities[row.key] ?? 0;
                                        final max = quantity + _remainingForSector(row.sector);
                                        return QuantityRow(
                                          title: row.ticketTypeName ?? row.sector.name,
                                          price: row.price,
                                          quantity: quantity,
                                          maxQuantity: max.clamp(0, _maxPerOrder),
                                          isSoldOut: row.sector.isSoldOut,
                                          note: row.sector.isLowStock
                                              ? 'Još ${row.sector.remainingCapacity} za ovaj dan'
                                              : null,
                                          onDecrement: () => setState(
                                            () => _quantities[row.key] =
                                                (quantity - 1).clamp(0, _maxPerOrder),
                                          ),
                                          onIncrement: () => setState(
                                            () => _quantities[row.key] =
                                                (quantity + 1).clamp(0, _maxPerOrder),
                                          ),
                                        );
                                      },
                                    ),
                                  ),
                              ],
                            ),
                          if (_submitError != null) ...[
                            const SizedBox(height: 8),
                            Text(
                              _submitError!,
                              style: const TextStyle(
                                color: AppColors.errorDark,
                                fontSize: 13,
                              ),
                            ),
                          ],
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
          if (_rows.isNotEmpty)
            Positioned(
              left: 0,
              right: 0,
              bottom: 0,
              child: PurchaseBottomBar(
                totalLabel: _selectedDate != null
                    ? _formatDate(_selectedDate!)
                    : 'Odaberite datum',
                total: _total,
                buttonLabel: _isSelectedDateSoldOut ? 'Rasprodano' : 'Kupi ulaznice',
                enabled: _hasSelection &&
                    _selectedDate != null &&
                    !_isSubmitting &&
                    !_isCheckingAvailability,
                isLoading: _isSubmitting,
                onPressed: _proceedToCheckout,
              ),
            ),
        ],
      ),
    );
  }
}

class _CalendarGrid extends StatelessWidget {
  final DateTime visibleMonth;
  final DateTime firstSelectableDay;
  final DateTime? selectedDate;
  final ValueChanged<DateTime> onSelect;

  const _CalendarGrid({
    required this.visibleMonth,
    required this.firstSelectableDay,
    required this.selectedDate,
    required this.onSelect,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark
        ? AppColors.darkTextTertiary
        : AppColors.lightTextTertiary;
    final disabledText = isDark
        ? AppColors.darkTextDisabled
        : AppColors.lightTextDisabled;
    final primary = Theme.of(context).colorScheme.primary;

    final daysInMonth = DateTime(
      visibleMonth.year,
      visibleMonth.month + 1,
      0,
    ).day;
    // Dart's weekday: Monday=1..Sunday=7 — matches the mockup's P U S Č P S N (Mon-first) header directly.
    final leadingBlanks =
        DateTime(visibleMonth.year, visibleMonth.month, 1).weekday - 1;

    return Column(
      children: [
        Row(
          children: [
            for (final label in _MuseumTicketScreenState._weekdayLabels)
              Expanded(
                child: Center(
                  child: Text(
                    label,
                    style: TextStyle(fontSize: 11, color: tertiaryText),
                  ),
                ),
              ),
          ],
        ),
        const SizedBox(height: 6),
        GridView.builder(
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          padding: EdgeInsets.zero,
          gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
            crossAxisCount: 7,
          ),
          itemCount: leadingBlanks + daysInMonth,
          itemBuilder: (context, index) {
            if (index < leadingBlanks) return const SizedBox.shrink();
            final day = index - leadingBlanks + 1;
            final date = DateTime(visibleMonth.year, visibleMonth.month, day);
            final isSelectable = !date.isBefore(firstSelectableDay);
            final isSelected =
                selectedDate != null &&
                selectedDate!.year == date.year &&
                selectedDate!.month == date.month &&
                selectedDate!.day == date.day;

            return InkWell(
              onTap: isSelectable ? () => onSelect(date) : null,
              borderRadius: BorderRadius.circular(999),
              child: Center(
                child: Container(
                  width: 30,
                  height: 30,
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    color: isSelected ? primary : Colors.transparent,
                  ),
                  child: Center(
                    child: Text(
                      '$day',
                      style: TextStyle(
                        fontSize: 13,
                        fontWeight: isSelected
                            ? FontWeight.w700
                            : FontWeight.w400,
                        color: isSelected
                            ? Colors.white
                            : (isSelectable ? null : disabledText),
                      ),
                    ),
                  ),
                ),
              ),
            );
          },
        ),
        const SizedBox(height: 12),
      ],
    );
  }
}
