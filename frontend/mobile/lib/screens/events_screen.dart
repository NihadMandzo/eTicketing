import 'dart:async';

import 'package:flutter/material.dart';

import '../core/session.dart';
import '../models/city.dart';
import '../models/responses/category_response.dart';
import '../models/responses/product_response.dart';
import '../models/responses/recommendation_response.dart';
import '../models/ticketing_mode.dart';
import '../services/api_exception.dart';
import '../services/catalog_service.dart';
import '../services/recommendation_service.dart';
import '../theme/app_colors.dart';
import '../widgets/product_card.dart';
import '../widgets/recommendation_row.dart';
import '../widgets/responsive_page.dart';
import 'event_details_screen.dart';
import 'museum_ticket_screen.dart';
import 'parking_spot_screen.dart';

/// "Događaji" tab body — search + filters + result list, branching on
/// `product.ticketingMode` (not category name) into the right details
/// screen, same rule as `frontend/web`'s products page.
///
/// **One scroll, two bars that stack as you reach them.** The whole page is a
/// single [CustomScrollView]. The brand row [header] and the recommendation
/// strip scroll away; the search field and the filter row are separate pinned
/// slivers, so they come to rest one under the other as the page passes them.
///
/// The filters sit *below* the recommendation strip, not above it, because they
/// belong to the results list they govern — putting them between the strip and
/// the list keeps "steer the results" and "here are the results" adjacent, and
/// leaves the strip as the browsing content it is. Both bars still end up
/// reachable at any scroll depth, which is the reason they are pinned at all.
class EventsScreen extends StatefulWidget {
  /// Rendered as the first thing in the scroll view, above the pinned control
  /// bar, and scrolls away with the content. [MainShell] passes its brand row
  /// (logo + avatar) here rather than stacking it outside this widget, which
  /// would have left it fixed and stolen the height permanently.
  final Widget? header;

  const EventsScreen({super.key, this.header});

  @override
  State<EventsScreen> createState() => _EventsScreenState();
}

class _EventsScreenState extends State<EventsScreen> {
  final _catalogService = CatalogService();
  final _recommendationService = RecommendationService();
  final _searchCtrl = TextEditingController();
  Timer? _debounce;

  List<CategoryResponse> _categories = [];
  int? _selectedCategoryId;
  City? _selectedCity;
  List<ProductResponse> _products = [];
  int _totalCount = 0;
  bool _isLoading = true;
  String? _errorMessage;

  List<ProductResponse> _recommendations = [];
  String _recommendationTitle = recommendationTitle(RecommendationSource.popular);
  bool _isLoadingRecommendations = true;

  /// The row belongs to the default browse state only. Once someone is searching or filtering they
  /// have told us what they want, and a "preporučeno za vas" strip above their results is in the
  /// way rather than helpful.
  bool get _showRecommendations =>
      _searchCtrl.text.trim().isEmpty && _selectedCategoryId == null && _selectedCity == null;

  @override
  void initState() {
    super.initState();
    _load();
    _loadRecommendations();
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
          city: _selectedCity,
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

  /// Loaded once, on entry, rather than on every filter change: the row is hidden while a filter
  /// is active anyway, and the recommendations themselves don't depend on the filters.
  ///
  /// Every failure just hides the row — recommendations are an enhancement to this screen, and a
  /// broken one must not cost the browsing experience anything.
  Future<void> _loadRecommendations() async {
    try {
      if (Session.isAuthenticated) {
        final result = await _recommendationService.getForMe(take: 8);
        if (!mounted) return;
        setState(() {
          _recommendations = result.items;
          _recommendationTitle = recommendationTitle(result.source);
          _isLoadingRecommendations = false;
        });
        return;
      }

      final popular = await _recommendationService.getPopular(take: 8);
      if (!mounted) return;
      setState(() {
        _recommendations = popular;
        _recommendationTitle = recommendationTitle(RecommendationSource.popular);
        _isLoadingRecommendations = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _recommendations = [];
        _isLoadingRecommendations = false;
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

  void _selectCity(City? city) {
    setState(() => _selectedCity = city);
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

    return RefreshIndicator(
      onRefresh: _load,
      child: ResponsivePage(
        padding: EdgeInsets.zero,
        child: CustomScrollView(
          // Keeps pull-to-refresh and the "nothing found" state reachable even when the list is
          // too short to scroll on its own.
          physics: const AlwaysScrollableScrollPhysics(),
          slivers: [
            if (widget.header != null) SliverToBoxAdapter(child: widget.header),
            SliverPersistentHeader(
              pinned: true,
              delegate: _SearchBarDelegate(
                controller: _searchCtrl,
                onChanged: _onSearchChanged,
                isDark: isDark,
              ),
            ),
            if (_showRecommendations)
              SliverToBoxAdapter(
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(16, 0, 16, 0),
                  child: RecommendationRow(
                    title: _recommendationTitle,
                    products: _recommendations,
                    isLoading: _isLoadingRecommendations,
                    onProductTap: _openProduct,
                  ),
                ),
              ),
            SliverPersistentHeader(
              pinned: true,
              delegate: _FilterBarDelegate(
                categories: _categories,
                selectedCategoryId: _selectedCategoryId,
                onCategoryChanged: _selectCategory,
                selectedCity: _selectedCity,
                onCityChanged: _selectCity,
                isDark: isDark,
              ),
            ),
            if (!_isLoading && _errorMessage == null)
              SliverToBoxAdapter(
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(20, 2, 20, 10),
                  child: Text(
                    '$_totalCount događaja',
                    style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: tertiaryText),
                  ),
                ),
              ),
            ..._buildBodySlivers(tertiaryText),
          ],
        ),
      ),
    );
  }

  List<Widget> _buildBodySlivers(Color tertiaryText) {
    if (_isLoading) {
      return const [
        SliverFillRemaining(
          hasScrollBody: false,
          child: Padding(padding: EdgeInsets.symmetric(vertical: 48), child: Center(child: CircularProgressIndicator())),
        ),
      ];
    }

    if (_errorMessage != null) {
      return [
        SliverFillRemaining(
          hasScrollBody: false,
          child: Padding(
            padding: const EdgeInsets.all(32),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(_errorMessage!, textAlign: TextAlign.center, style: TextStyle(color: tertiaryText)),
                const SizedBox(height: 12),
                OutlinedButton(onPressed: _load, child: const Text('Pokušaj ponovo')),
              ],
            ),
          ),
        ),
      ];
    }

    if (_products.isEmpty) {
      return [
        SliverFillRemaining(
          hasScrollBody: false,
          child: Padding(
            padding: const EdgeInsets.all(32),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(Icons.search_off_rounded, size: 32, color: tertiaryText),
                const SizedBox(height: 12),
                Text(
                  'Nijedan događaj ne odgovara pretrazi.',
                  textAlign: TextAlign.center,
                  style: TextStyle(color: tertiaryText),
                ),
                const SizedBox(height: 4),
                Text(
                  'Promijenite pojam ili poništite filtere.',
                  textAlign: TextAlign.center,
                  style: TextStyle(fontSize: 12, color: tertiaryText),
                ),
              ],
            ),
          ),
        ),
      ];
    }

    return [
      SliverPadding(
        padding: const EdgeInsets.fromLTRB(20, 0, 20, 24),
        sliver: SliverList.separated(
          itemCount: _products.length,
          separatorBuilder: (_, _) => const SizedBox(height: 16),
          itemBuilder: (context, index) =>
              ProductCard(product: _products[index], onTap: () => _openProduct(_products[index])),
        ),
      ),
    ];
  }
}

/// The search field, pinned to the very top of the list.
///
/// Fixed height (no collapse-on-scroll): the bar is only as tall as the field itself, and shrinking
/// a text field mid-scroll makes the caret jump. It paints an opaque background at all times — a
/// translucent one would let cards scroll visibly through the text — and grows a hairline only once
/// there is content passing beneath it, so the separation appears exactly when it means something.
class _SearchBarDelegate extends SliverPersistentHeaderDelegate {
  final TextEditingController controller;
  final ValueChanged<String> onChanged;
  final bool isDark;

  const _SearchBarDelegate({required this.controller, required this.onChanged, required this.isDark});

  static const double _height = 58;

  @override
  double get minExtent => _height;

  @override
  double get maxExtent => _height;

  @override
  Widget build(BuildContext context, double shrinkOffset, bool overlapsContent) {
    return _PinnedBar(
      isDark: isDark,
      height: _height,
      showDivider: overlapsContent,
      child: TextField(
        controller: controller,
        onChanged: onChanged,
        textInputAction: TextInputAction.search,
        decoration: InputDecoration(
          hintText: 'Pretraži događaje',
          prefixIcon: const Icon(Icons.search_rounded, size: 20),
          filled: true,
          fillColor: isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted,
          border: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide.none),
          enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide.none),
          contentPadding: EdgeInsets.zero,
        ),
      ),
    );
  }

  @override
  bool shouldRebuild(covariant _SearchBarDelegate old) => old.isDark != isDark;
}

/// The category and location dropdowns, pinned directly under the search field once the
/// recommendation strip between them has scrolled past.
class _FilterBarDelegate extends SliverPersistentHeaderDelegate {
  final List<CategoryResponse> categories;
  final int? selectedCategoryId;
  final ValueChanged<int?> onCategoryChanged;
  final City? selectedCity;
  final ValueChanged<City?> onCityChanged;
  final bool isDark;

  const _FilterBarDelegate({
    required this.categories,
    required this.selectedCategoryId,
    required this.onCategoryChanged,
    required this.selectedCity,
    required this.onCityChanged,
    required this.isDark,
  });

  static const double _height = 52;

  @override
  double get minExtent => _height;

  @override
  double get maxExtent => _height;

  @override
  Widget build(BuildContext context, double shrinkOffset, bool overlapsContent) {
    return _PinnedBar(
      isDark: isDark,
      height: _height,
      showDivider: overlapsContent,
      child: Row(
        children: [
          Expanded(
            child: _FilterDropdown<int?>(
              hint: 'Sve kategorije',
              value: selectedCategoryId,
              items: [
                const DropdownMenuItem(value: null, child: Text('Sve kategorije')),
                for (final category in categories)
                  DropdownMenuItem(value: category.id, child: Text(category.name, overflow: TextOverflow.ellipsis)),
              ],
              onChanged: onCategoryChanged,
            ),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: _FilterDropdown<City?>(
              hint: 'Sve lokacije',
              value: selectedCity,
              items: [
                const DropdownMenuItem(value: null, child: Text('Sve lokacije')),
                for (final city in City.values)
                  DropdownMenuItem(value: city, child: Text(cityLabel(city), overflow: TextOverflow.ellipsis)),
              ],
              onChanged: onCityChanged,
            ),
          ),
        ],
      ),
    );
  }

  @override
  bool shouldRebuild(covariant _FilterBarDelegate old) =>
      old.categories.length != categories.length ||
      old.selectedCategoryId != selectedCategoryId ||
      old.selectedCity != selectedCity ||
      old.isDark != isDark;
}

/// Shared chrome for both pinned bars, so the two cannot drift apart in background, padding or
/// divider treatment while sitting directly on top of each other.
class _PinnedBar extends StatelessWidget {
  final bool isDark;
  final double height;
  final bool showDivider;
  final Widget child;

  const _PinnedBar({
    required this.isDark,
    required this.height,
    required this.showDivider,
    required this.child,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      height: height,
      color: isDark ? AppColors.darkBackground : AppColors.lightBackground,
      padding: const EdgeInsets.fromLTRB(20, 0, 20, 12),
      foregroundDecoration: showDivider
          ? BoxDecoration(
              border: Border(bottom: BorderSide(color: isDark ? AppColors.darkBorder : AppColors.lightBorder)),
            )
          : null,
      child: child,
    );
  }
}

/// Category/location filter dropdown — replaces the old pill-chip row (single-select only fit a
/// handful of categories on screen at once; a dropdown scales to any number of categories/cities
/// and leaves room for both filters side by side). Styled to match the search field just above it
/// (filled, no border, 12px radius), matching this app's mockup component language. An active
/// filter takes the brand colour, so the bar says what it is doing without a separate badge.
class _FilterDropdown<T> extends StatelessWidget {
  final String hint;
  final T value;
  final List<DropdownMenuItem<T>> items;
  final ValueChanged<T> onChanged;

  const _FilterDropdown({required this.hint, required this.value, required this.items, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primary = Theme.of(context).colorScheme.primary;
    final isActive = value != null;
    final textColor = isActive ? primary : (isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary);

    return Container(
      height: 40,
      padding: const EdgeInsets.symmetric(horizontal: 12),
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted,
        borderRadius: BorderRadius.circular(12),
        border: isActive ? Border.all(color: primary, width: 1.5) : null,
      ),
      child: DropdownButtonHideUnderline(
        child: DropdownButton<T>(
          value: value,
          isExpanded: true,
          hint: Text(hint, style: const TextStyle(fontSize: 13)),
          icon: Icon(Icons.keyboard_arrow_down_rounded, size: 18, color: textColor),
          style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: textColor),
          items: items,
          onChanged: (v) => onChanged(v as T),
        ),
      ),
    );
  }
}
