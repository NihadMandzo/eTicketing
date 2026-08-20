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
import '../widgets/purchase_widgets.dart';
import 'login_screen.dart';
import 'payment_screen.dart';

class _TicketTypeRow {
  final SectorResponse sector;
  final String? ticketTypeId;
  final String? ticketTypeName;
  final double price;

  const _TicketTypeRow({required this.sector, this.ticketTypeId, this.ticketTypeName, required this.price});

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
  bool _isSubmitting = false;
  String? _submitError;

  @override
  void initState() {
    super.initState();
    final tomorrow = DateTime.now().add(const Duration(days: 1));
    _visibleMonth = DateTime(tomorrow.year, tomorrow.month);
    _selectedDate = DateTime(tomorrow.year, tomorrow.month, tomorrow.day);
    _load();
  }

  Future<void> _load() async {
    try {
      final product = await _catalogService.getProductById(widget.productId);
      final sectors = await _sectorService.getSectors(widget.productId);
      OrganizationResponse? org;
      try {
        org = await _organizationService.getById(product.organizationId);
      } catch (_) {}
      if (!mounted) return;
      setState(() {
        _product = product;
        _sectors = sectors.items;
        _organization = org;
        _isLoading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _loadError = 'Događaj nije pronađen ili više nije dostupan.';
        _isLoading = false;
      });
    }
  }

  List<_TicketTypeRow> get _rows => _sectors.expand((sector) {
        if (sector.ticketTypes.isNotEmpty) {
          return sector.ticketTypes
              .map((tt) => _TicketTypeRow(sector: sector, ticketTypeId: tt.id, ticketTypeName: tt.name, price: tt.price));
        }
        return [_TicketTypeRow(sector: sector, price: sector.price)];
      }).toList();

  double get _total => _rows.fold(0, (sum, row) => sum + (_quantities[row.key] ?? 0) * row.price);

  bool get _hasSelection => _quantities.values.any((q) => q > 0);

  void _changeMonth(int delta) {
    setState(() {
      _visibleMonth = DateTime(_visibleMonth.year, _visibleMonth.month + delta);
      _selectedDate = null;
    });
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

    try {
      final holds = <CartHoldGroup>[];
      for (final entry in bySector.entries) {
        final quantity = entry.value.fold(0, (sum, row) => sum + (_quantities[row.key] ?? 0));
        final hold = await _sectorService.hold(entry.key, HoldSectorRequest(quantity: quantity, date: dateStr));
        holds.add(CartHoldGroup(
          holdId: hold.holdId,
          sectorId: entry.key,
          sectorName: entry.value.first.sector.name,
          lineItems: entry.value
              .map((row) => CartLineItem(
                    ticketTypeId: row.ticketTypeId,
                    ticketTypeName: row.ticketTypeName,
                    quantity: _quantities[row.key] ?? 0,
                    unitPrice: row.price,
                  ))
              .toList(),
        ));
      }

      Cart.set(CartState(
        productId: product.id,
        productName: product.name,
        eventSummary: _formatDate(date),
        date: dateStr,
        holds: holds,
      ));

      if (!mounted) return;
      Navigator.of(context).push(MaterialPageRoute(builder: (_) => const PaymentScreen()));
    } on ApiException catch (e) {
      if (!mounted) return;
      if (e.statusCode == 401) {
        Navigator.of(context).pushAndRemoveUntil(MaterialPageRoute(builder: (_) => const LoginScreen()), (route) => false);
        return;
      }
      setState(() => _submitError = e.apiError.displayMessage);
    } catch (_) {
      if (mounted) setState(() => _submitError = 'Došlo je do greške. Pokušajte ponovo.');
    } finally {
      if (mounted) setState(() => _isSubmitting = false);
    }
  }

  static const _months = [
    'Januar', 'Februar', 'Mart', 'April', 'Maj', 'Juni', 'Juli', 'August', 'Septembar', 'Oktobar', 'Novembar', 'Decembar',
  ];
  static const _weekdayLabels = ['P', 'U', 'S', 'Č', 'P', 'S', 'N'];

  static String _formatDate(DateTime date) => '${date.day}. ${_months[date.month - 1]} ${date.year}.';

  @override
  Widget build(BuildContext context) {
    if (_isLoading) return const Scaffold(body: Center(child: CircularProgressIndicator()));
    if (_loadError != null || _product == null) {
      return Scaffold(appBar: AppBar(title: const Text('Muzejska ulaznica')), body: Center(child: Text(_loadError ?? 'Greška')));
    }

    final product = _product!;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final tomorrow = DateTime.now().add(const Duration(days: 1));
    final firstSelectableDay = DateTime(tomorrow.year, tomorrow.month, tomorrow.day);

    return Scaffold(
      body: Stack(
        children: [
          SafeArea(
            bottom: false,
            child: SingleChildScrollView(
              padding: const EdgeInsets.only(bottom: 100),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Stack(
                    children: [
                      Container(
                        height: 200,
                        width: double.infinity,
                        decoration: const BoxDecoration(
                          gradient: LinearGradient(colors: [AppColors.primary, AppColors.secondary], begin: Alignment.topLeft, end: Alignment.bottomRight),
                        ),
                      ),
                      Positioned(top: 0, left: 0, child: CircleBackButton(onTap: () => Navigator.of(context).pop())),
                    ],
                  ),
                  Padding(
                    padding: const EdgeInsets.fromLTRB(20, 20, 20, 0),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(children: [
                          Container(
                            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                            decoration: BoxDecoration(color: AppColors.accent, borderRadius: BorderRadius.circular(6)),
                            child: Text(product.categoryName, style: const TextStyle(color: Colors.white, fontSize: 11, fontWeight: FontWeight.w700)),
                          ),
                        ]),
                        const SizedBox(height: 12),
                        Text(product.name, style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w700)),
                        if (_organization != null) ...[
                          const SizedBox(height: 12),
                          InfoRow(icon: Icons.person_outline_rounded, text: 'Organizator: ${_organization!.name}'),
                        ],
                        const SizedBox(height: 20),
                        const Text('Odaberite datum posjete', style: TextStyle(fontSize: 15, fontWeight: FontWeight.w700)),
                        const SizedBox(height: 12),
                        Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            IconButton(icon: const Icon(Icons.chevron_left_rounded), onPressed: () => _changeMonth(-1)),
                            Text('${_months[_visibleMonth.month - 1]} ${_visibleMonth.year}', style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w700)),
                            IconButton(icon: const Icon(Icons.chevron_right_rounded), onPressed: () => _changeMonth(1)),
                          ],
                        ),
                        _CalendarGrid(
                          visibleMonth: _visibleMonth,
                          firstSelectableDay: firstSelectableDay,
                          selectedDate: _selectedDate,
                          onSelect: (d) => setState(() => _selectedDate = d),
                        ),
                        const SizedBox(height: 12),
                        const Text('Vrsta ulaznice', style: TextStyle(fontSize: 15, fontWeight: FontWeight.w700)),
                        const SizedBox(height: 12),
                        if (_rows.isEmpty)
                          Text('Nema dostupnih ulaznica za ovaj muzej.', style: TextStyle(color: tertiaryText))
                        else
                          Column(children: [
                            for (final row in _rows)
                              Padding(
                                padding: const EdgeInsets.only(bottom: 14),
                                child: QuantityRow(
                                  title: row.ticketTypeName ?? row.sector.name,
                                  price: row.price,
                                  quantity: _quantities[row.key] ?? 0,
                                  onDecrement: () => setState(() => _quantities[row.key] = ((_quantities[row.key] ?? 0) - 1).clamp(0, 10)),
                                  onIncrement: () => setState(() => _quantities[row.key] = ((_quantities[row.key] ?? 0) + 1).clamp(0, 10)),
                                ),
                              ),
                          ]),
                        if (_submitError != null) ...[
                          const SizedBox(height: 8),
                          Text(_submitError!, style: const TextStyle(color: AppColors.errorDark, fontSize: 13)),
                        ],
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
          if (_rows.isNotEmpty)
            Positioned(
              left: 0,
              right: 0,
              bottom: 0,
              child: PurchaseBottomBar(
                totalLabel: _selectedDate != null ? _formatDate(_selectedDate!) : 'Odaberite datum',
                total: _total,
                buttonLabel: 'Kupi ulaznice',
                enabled: _hasSelection && _selectedDate != null && !_isSubmitting,
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
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final disabledText = isDark ? AppColors.darkTextDisabled : AppColors.lightTextDisabled;
    final primary = Theme.of(context).colorScheme.primary;

    final daysInMonth = DateTime(visibleMonth.year, visibleMonth.month + 1, 0).day;
    // Dart's weekday: Monday=1..Sunday=7 — matches the mockup's P U S Č P S N (Mon-first) header directly.
    final leadingBlanks = DateTime(visibleMonth.year, visibleMonth.month, 1).weekday - 1;

    return Column(
      children: [
        Row(
          children: [
            for (final label in _MuseumTicketScreenState._weekdayLabels)
              Expanded(child: Center(child: Text(label, style: TextStyle(fontSize: 11, color: tertiaryText)))),
          ],
        ),
        const SizedBox(height: 6),
        GridView.builder(
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          padding: EdgeInsets.zero,
          gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(crossAxisCount: 7),
          itemCount: leadingBlanks + daysInMonth,
          itemBuilder: (context, index) {
            if (index < leadingBlanks) return const SizedBox.shrink();
            final day = index - leadingBlanks + 1;
            final date = DateTime(visibleMonth.year, visibleMonth.month, day);
            final isSelectable = !date.isBefore(firstSelectableDay);
            final isSelected = selectedDate != null &&
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
                  decoration: BoxDecoration(shape: BoxShape.circle, color: isSelected ? primary : Colors.transparent),
                  child: Center(
                    child: Text(
                      '$day',
                      style: TextStyle(
                        fontSize: 13,
                        fontWeight: isSelected ? FontWeight.w700 : FontWeight.w400,
                        color: isSelected ? Colors.white : (isSelectable ? null : disabledText),
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
