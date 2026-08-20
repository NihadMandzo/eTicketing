import 'dart:async';

import 'package:flutter/material.dart';

import '../models/responses/category_response.dart';
import '../models/responses/product_response.dart';
import '../models/ticketing_mode.dart';
import '../services/api_exception.dart';
import '../services/catalog_service.dart';
import '../theme/app_colors.dart';
import 'event_details_screen.dart';
import 'museum_ticket_screen.dart';
import 'parking_spot_screen.dart';

/// "Događaji" tab body — search + category chips + result list, branching on
/// `product.ticketingMode` (not category name) into the right details
/// screen, same rule as `frontend/web`'s products page. Rendered inside
/// [MainShell]'s `_EventsTab`, which already supplies the brand-row header —
/// this widget starts straight at the search bar (mockup screen 2).
class EventsScreen extends StatefulWidget {
  const EventsScreen({super.key});

  @override
  State<EventsScreen> createState() => _EventsScreenState();
}

class _EventsScreenState extends State<EventsScreen> {
  final _catalogService = CatalogService();
  final _searchCtrl = TextEditingController();
  Timer? _debounce;

  List<CategoryResponse> _categories = [];
  int? _selectedCategoryId;
  List<ProductResponse> _products = [];
  int _totalCount = 0;
  bool _isLoading = true;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _searchCtrl.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });
    try {
      final results = await Future.wait([
        _catalogService.getCategories(),
        _catalogService.getProducts(
          page: 0,
          pageSize: 30,
          fts: _searchCtrl.text.trim(),
          categoryId: _selectedCategoryId,
        ),
      ]);
      if (!mounted) return;
      setState(() {
        _categories = results[0] as List<CategoryResponse>;
        final paged = results[1] as dynamic;
        _products = List<ProductResponse>.from(paged.items as List<ProductResponse>);
        _totalCount = paged.totalCount as int;
        _isLoading = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _errorMessage = e.apiError.displayMessage;
        _isLoading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _errorMessage = 'Događaje nije moguće učitati. Pokušajte ponovo.';
        _isLoading = false;
      });
    }
  }

  void _onSearchChanged(String _) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 400), _load);
  }

  void _selectCategory(int? id) {
    setState(() => _selectedCategoryId = id);
    _load();
  }

  void _openProduct(ProductResponse product) {
    final route = switch (product.ticketingMode) {
      TicketingMode.dailyEntry => MaterialPageRoute(builder: (_) => MuseumTicketScreen(productId: product.id)),
      TicketingMode.recurringReservation => MaterialPageRoute(builder: (_) => ParkingSpotScreen(productId: product.id)),
      TicketingMode.singleOccurrence => MaterialPageRoute(builder: (_) => EventDetailsScreen(productId: product.id)),
    };
    Navigator.of(context).push(route);
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;

    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(20, 0, 20, 12),
          child: TextField(
            controller: _searchCtrl,
            onChanged: _onSearchChanged,
            decoration: InputDecoration(
              hintText: 'Pretraži događaje',
              prefixIcon: const Icon(Icons.search_rounded),
              filled: true,
              fillColor: isDark ? AppColors.darkSurfaceMuted : const Color(0xFFF5F5F5),
              border: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide.none),
              contentPadding: const EdgeInsets.symmetric(vertical: 12),
            ),
          ),
        ),
        SizedBox(
          height: 36,
          child: ListView(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: 20),
            children: [
              _CategoryChip(label: 'Sve', selected: _selectedCategoryId == null, onTap: () => _selectCategory(null)),
              for (final category in _categories)
                Padding(
                  padding: const EdgeInsets.only(left: 8),
                  child: _CategoryChip(
                    label: category.name,
                    selected: _selectedCategoryId == category.id,
                    onTap: () => _selectCategory(category.id),
                  ),
                ),
            ],
          ),
        ),
        const SizedBox(height: 12),
        if (!_isLoading && _errorMessage == null)
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 0, 20, 10),
            child: Align(
              alignment: Alignment.centerLeft,
              child: Text('$_totalCount događaja', style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: tertiaryText)),
            ),
          ),
        Expanded(child: _buildBody(tertiaryText)),
      ],
    );
  }

  Widget _buildBody(Color tertiaryText) {
    if (_isLoading) return const Center(child: CircularProgressIndicator());
    if (_errorMessage != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(_errorMessage!, textAlign: TextAlign.center, style: TextStyle(color: tertiaryText)),
              const SizedBox(height: 12),
              OutlinedButton(onPressed: _load, child: const Text('Pokušaj ponovo')),
            ],
          ),
        ),
      );
    }
    if (_products.isEmpty) {
      return Center(
        child: Text('Nema pronađenih događaja.', style: TextStyle(color: tertiaryText)),
      );
    }
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView.separated(
        padding: const EdgeInsets.fromLTRB(20, 0, 20, 16),
        itemCount: _products.length,
        separatorBuilder: (_, _) => const SizedBox(height: 16),
        itemBuilder: (context, index) => _ProductCard(product: _products[index], onTap: () => _openProduct(_products[index])),
      ),
    );
  }
}

class _CategoryChip extends StatelessWidget {
  final String label;
  final bool selected;
  final VoidCallback onTap;

  const _CategoryChip({required this.label, required this.selected, required this.onTap});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primary = Theme.of(context).colorScheme.primary;
    return InkWell(
      borderRadius: BorderRadius.circular(999),
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        decoration: BoxDecoration(
          color: selected ? primary : (isDark ? AppColors.darkSurfaceMuted : const Color(0xFFF5F5F5)),
          borderRadius: BorderRadius.circular(999),
        ),
        child: Text(
          label,
          style: TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w600,
            color: selected ? Colors.white : (isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary),
          ),
        ),
      ),
    );
  }
}

/// Deliberately omits a price/location teaser — `Product` carries neither
/// (only `Sector` has a price, and there is no venue/location field), same
/// reasoning as `ProductCardComponent` on `frontend/web`.
class _ProductCard extends StatelessWidget {
  final ProductResponse product;
  final VoidCallback onTap;

  const _ProductCard({required this.product, required this.onTap});

  String get _subtitle => switch (product.ticketingMode) {
        TicketingMode.dailyEntry => 'Otvoreno svaki dan',
        TicketingMode.recurringReservation => 'Mjesečna pretplata',
        TicketingMode.singleOccurrence =>
          product.date != null ? _formatDate(product.date!) : 'Datum nije naveden',
      };

  static String _formatDate(DateTime date) {
    const months = [
      'Januar', 'Februar', 'Mart', 'April', 'Maj', 'Juni', 'Juli', 'August', 'Septembar', 'Oktobar', 'Novembar', 'Decembar',
    ];
    return '${date.day} ${months[date.month - 1]} ${date.year}';
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final primary = Theme.of(context).colorScheme.primary;

    return InkWell(
      borderRadius: BorderRadius.circular(16),
      onTap: onTap,
      child: Card(
        clipBehavior: Clip.antiAlias,
        margin: EdgeInsets.zero,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Stack(
              children: [
                Container(
                  height: 130,
                  decoration: const BoxDecoration(
                    gradient: LinearGradient(
                      colors: [AppColors.primary, AppColors.secondary],
                      begin: Alignment.topLeft,
                      end: Alignment.bottomRight,
                    ),
                  ),
                  child: product.images.isNotEmpty
                      ? Image.network(product.images.first.url, fit: BoxFit.cover, errorBuilder: (_, _, _) => const SizedBox.shrink())
                      : null,
                ),
                Positioned(
                  top: 10,
                  right: 10,
                  child: Container(
                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                    decoration: BoxDecoration(color: AppColors.accent, borderRadius: BorderRadius.circular(6)),
                    child: Text(
                      product.categoryName,
                      style: const TextStyle(color: Colors.white, fontSize: 11, fontWeight: FontWeight.w700),
                    ),
                  ),
                ),
              ],
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 14, 16, 14),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(_subtitle, style: TextStyle(fontSize: 12, fontWeight: FontWeight.w700, color: primary)),
                  const SizedBox(height: 4),
                  Text(product.name, style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700)),
                  const SizedBox(height: 10),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text('Pogledaj detalje', style: TextStyle(fontSize: 13, color: tertiaryText)),
                      Container(
                        width: 28,
                        height: 28,
                        decoration: BoxDecoration(
                          color: isDark ? AppColors.darkSurfaceMuted : const Color(0xFFF5F5F5),
                          shape: BoxShape.circle,
                        ),
                        child: const Icon(Icons.chevron_right_rounded, size: 18),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
