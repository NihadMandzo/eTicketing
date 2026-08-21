import 'dart:async';

import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../models/enums/city.dart';
import '../models/responses/category_response.dart';
import '../models/responses/product_response.dart';
import '../models/search_objects/base_search_object.dart';
import '../models/search_objects/product_search_object.dart';
import '../providers/category_provider.dart';
import '../providers/product_provider.dart';
import '../theme/app_colors.dart';
import '../widgets/confirm_dialog.dart';
import 'product_detail_screen.dart';
import 'widgets/pagination_bar.dart';
import 'widgets/paginated_screen_body.dart';
import 'widgets/product_upsert_dialog.dart';
import '../main.dart';

/// Organizer's own products (Organization*Admin) — full create/preview/
/// publish/edit/delete, generalized across TicketingMode. Mirrors
/// CategoriesScreen's layout/pagination pattern. PlatformStaff instead
/// browses/edits every organization's products through
/// OrganizationDetailScreen's Products tab, not this screen (see
/// .claude/rules/01-domain.md).
class ProductsScreen extends StatefulWidget {
  const ProductsScreen({super.key});

  @override
  State<ProductsScreen> createState() => _ProductsScreenState();
}

class _ProductsScreenState extends State<ProductsScreen> {
  final _searchController = TextEditingController();
  final _provider = ProductProvider();
  Timer? _debounce;
  StreamSubscription<void>? _categoryRefreshSubscription;

  List<ProductResponse> _products = [];
  bool _isLoading = true;

  List<CategoryResponse> _categories = [];
  int? _selectedCategoryId;
  City? _selectedCity;

  int _currentPage = 0;
  int _totalCount = 0;
  int _pageSize = 10;

  int get _totalPages => (_totalCount / _pageSize).ceil().clamp(1, 99999);

  bool get _isDark => Theme.of(context).brightness == Brightness.dark;

  @override
  void initState() {
    super.initState();
    _loadCategories();
    _loadData();
    // Refetch whenever a category is created/edited/deleted anywhere else in the app — see
    // CategoryProvider.categoryRefreshBus.
    _categoryRefreshSubscription = CategoryProvider.categoryRefreshBus.stream.listen((_) => _loadCategories());
  }

  Future<void> _loadCategories() async {
    try {
      // Same "page through the server's 100-item ceiling" pattern as
      // CategoryMultiSelectFilter/ProductUpsertDialog — see their comments for why.
      final provider = CategoryProvider();
      final all = <CategoryResponse>[];
      var page = 0;
      const pageSize = 100;
      while (true) {
        final result = await provider.getAll(
          searchObject: BaseSearchObject(page: page, pageSize: pageSize),
          fromJson: CategoryResponse.fromJson,
        );
        all.addAll(result.items);
        if (result.items.isEmpty || all.length >= result.totalCount) break;
        page++;
      }
      if (mounted) setState(() => _categories = all);
    } catch (_) {
      // Non-fatal — the category filter just stays empty; search/location filters
      // still work. Product loading below surfaces its own errors.
    }
  }

  void _onSearchChanged(String _) {
    if (_debounce?.isActive ?? false) _debounce!.cancel();
    _debounce = Timer(const Duration(milliseconds: 300), () {
      setState(() => _currentPage = 0);
      _loadData();
    });
  }

  void _onCategoryChanged(int? categoryId) {
    setState(() {
      _selectedCategoryId = categoryId;
      _currentPage = 0;
    });
    _loadData();
  }

  void _onCityChanged(City? city) {
    setState(() {
      _selectedCity = city;
      _currentPage = 0;
    });
    _loadData();
  }

  Future<void> _loadData() async {
    setState(() => _isLoading = true);
    try {
      final searchObject = ProductSearchObject(
        page: _currentPage,
        pageSize: _pageSize,
        fts: _searchController.text.trim().isEmpty ? null : _searchController.text.trim(),
        categoryId: _selectedCategoryId,
        city: _selectedCity,
      );
      final result = await _provider.getMine(searchObject: searchObject);
      if (mounted) {
        setState(() {
          _products = result.items;
          _totalCount = result.totalCount;
          _isLoading = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() => _isLoading = false);
        handleApiError(e);
      }
    }
  }

  void _openCreateDialog() {
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => ProductUpsertDialog(onSaved: (_) => _loadData()),
    );
  }

  Future<void> _openDetail(ProductResponse product) async {
    await Navigator.of(context).push(
      MaterialPageRoute(builder: (_) => ProductDetailScreen(product: product)),
    );
    // The detail screen may have edited/published/deleted the product.
    if (mounted) _loadData();
  }

  Future<void> _deleteProduct(ProductResponse product) async {
    final confirmed = await ConfirmDialog.show(
      context,
      title: 'Obriši proizvod',
      message: 'Da li ste sigurni da želite obrisati proizvod "${product.name}"?',
      confirmLabel: 'Obriši',
    );
    if (confirmed != true || !mounted) return;

    try {
      await _provider.delete(product.id);
      if (!mounted) return;

      if (_products.length == 1 && _currentPage > 0) {
        setState(() => _currentPage--);
      }
      await _loadData();
    } catch (e) {
      if (mounted) handleApiError(e);
    }
  }

  void _goToPage(int page) {
    if (page < 0 || page >= _totalPages) return;
    setState(() => _currentPage = page);
    _loadData();
  }

  void _onPageSizeChanged(int size) {
    setState(() {
      _pageSize = size;
      _currentPage = 0;
    });
    _loadData();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _categoryRefreshSubscription?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final isDark = _isDark;
    final primary = isDark ? AppColors.secondary : AppColors.primary;
    final primaryDark = isDark ? AppColors.primary : AppColors.primaryDark;

    // The pagination bar is pinned to the bottom of the viewport (see
    // PaginatedScreenBody) — only the header/filters/count/grid above it scroll.
    return PaginatedScreenBody(
      pagination: PaginationBar(
        currentPage: _currentPage,
        totalPages: _totalPages,
        onPageChanged: _goToPage,
        pageSize: _pageSize,
        onPageSizeChanged: _onPageSizeChanged,
      ),
      content: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // ── Header ──
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Proizvodi',
                      style: TextStyle(
                        fontSize: 30,
                        fontWeight: FontWeight.w700,
                        color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
                        height: 1.2,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      'Upravljajte događajima, ulaznicama i rezervacijama vaše organizacije',
                      style: TextStyle(
                          fontSize: 16, color: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 16),
              Container(
                decoration: BoxDecoration(
                  gradient: LinearGradient(colors: [primary, primaryDark]),
                  borderRadius: BorderRadius.circular(12),
                  boxShadow: [
                    BoxShadow(color: primary.withValues(alpha: 0.2), blurRadius: 10, offset: const Offset(0, 4)),
                  ],
                ),
                child: Material(
                  color: Colors.transparent,
                  child: InkWell(
                    borderRadius: BorderRadius.circular(12),
                    onTap: _openCreateDialog,
                    child: const Padding(
                      padding: EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                      child: Row(
                        children: [
                          Icon(LucideIcons.plus, color: Colors.white, size: 20),
                          SizedBox(width: 8),
                          Text('Novi Proizvod', style: TextStyle(color: Colors.white, fontWeight: FontWeight.w600)),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
            ],
          ),

          const SizedBox(height: 24),

          // ── Search + category + location filters — LayoutBuilder-driven: side by
          // side above ~760px, stacked below so narrow windows never overflow.
          LayoutBuilder(
            builder: (context, constraints) {
              final searchField = Container(
                height: 44,
                decoration: BoxDecoration(
                  color: isDark ? AppColors.darkSurface : Colors.white,
                  border: Border.all(color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: TextField(
                  controller: _searchController,
                  onChanged: _onSearchChanged,
                  decoration: InputDecoration(
                    hintText: 'Pretražite proizvode...',
                    hintStyle: TextStyle(color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary),
                    prefixIcon: Icon(LucideIcons.search,
                        color: isDark ? AppColors.darkTextDisabled : AppColors.lightTextDisabled, size: 18),
                    border: InputBorder.none,
                    contentPadding: const EdgeInsets.symmetric(vertical: 12),
                  ),
                  style: TextStyle(color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary),
                ),
              );

              final categoryFilter = _FilterDropdown<int?>(
                hint: 'Sve kategorije',
                icon: LucideIcons.tag,
                value: _selectedCategoryId,
                items: [
                  const DropdownMenuItem(value: null, child: Text('Sve kategorije')),
                  for (final category in _categories)
                    DropdownMenuItem(value: category.id, child: Text(category.name, overflow: TextOverflow.ellipsis)),
                ],
                onChanged: _onCategoryChanged,
              );

              final cityFilter = _FilterDropdown<City?>(
                hint: 'Sve lokacije',
                icon: LucideIcons.mapPin,
                value: _selectedCity,
                items: [
                  const DropdownMenuItem(value: null, child: Text('Sve lokacije')),
                  for (final city in City.values)
                    DropdownMenuItem(value: city, child: Text(city.label, overflow: TextOverflow.ellipsis)),
                ],
                onChanged: _onCityChanged,
              );

              if (constraints.maxWidth >= 760) {
                return Row(
                  children: [
                    Expanded(flex: 2, child: searchField),
                    const SizedBox(width: 12),
                    Expanded(child: categoryFilter),
                    const SizedBox(width: 12),
                    Expanded(child: cityFilter),
                  ],
                );
              }
              return Column(
                children: [
                  searchField,
                  const SizedBox(height: 10),
                  Row(
                    children: [
                      Expanded(child: categoryFilter),
                      const SizedBox(width: 10),
                      Expanded(child: cityFilter),
                    ],
                  ),
                ],
              );
            },
          ),

          const SizedBox(height: 12),

          if (!_isLoading)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Text(
                '$_totalCount proizvoda',
                style: TextStyle(fontSize: 13, color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary),
              ),
            ),

          if (_isLoading)
            SizedBox(height: 300, child: Center(child: CircularProgressIndicator(color: primary)))
          else if (_products.isEmpty)
            SizedBox(
              height: 300,
              child: Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(LucideIcons.packageSearch,
                        size: 48, color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
                    const SizedBox(height: 12),
                    Text('Nema proizvoda',
                        style: TextStyle(
                            color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary, fontSize: 16)),
                  ],
                ),
              ),
            )
          else
            GridView.builder(
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              gridDelegate: const SliverGridDelegateWithMaxCrossAxisExtent(
                maxCrossAxisExtent: 280,
                mainAxisExtent: 320,
                crossAxisSpacing: 14,
                mainAxisSpacing: 14,
              ),
              itemCount: _products.length,
              itemBuilder: (context, index) {
                final product = _products[index];
                return _ProductCard(
                  product: product,
                  isDark: isDark,
                  onView: () => _openDetail(product),
                  onDelete: () => _deleteProduct(product),
                );
              },
            ),
        ],
      ),
    );
  }
}

/// Search/category/location filter dropdown — matches the mobile/web "3-filter toolbar"
/// treatment. Styled to sit flush next to the search field (same height/radius/border).
class _FilterDropdown<T> extends StatelessWidget {
  final String hint;
  final IconData icon;
  final T value;
  final List<DropdownMenuItem<T>> items;
  final ValueChanged<T> onChanged;

  const _FilterDropdown({
    required this.hint,
    required this.icon,
    required this.value,
    required this.items,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Container(
      height: 44,
      padding: const EdgeInsets.symmetric(horizontal: 12),
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurface : Colors.white,
        border: Border.all(color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        children: [
          Icon(icon, size: 16, color: isDark ? AppColors.darkTextDisabled : AppColors.lightTextDisabled),
          const SizedBox(width: 8),
          Expanded(
            child: DropdownButtonHideUnderline(
              child: DropdownButton<T>(
                value: value,
                isExpanded: true,
                isDense: true,
                hint: Text(hint, style: TextStyle(fontSize: 13, color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary)),
                icon: Icon(LucideIcons.chevronDown, size: 14, color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary),
                dropdownColor: isDark ? AppColors.darkSurface : Colors.white,
                style: TextStyle(fontSize: 13, fontWeight: FontWeight.w500, color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary),
                items: items,
                onChanged: (v) => onChanged(v as T),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _ProductCard extends StatefulWidget {
  final ProductResponse product;
  final bool isDark;
  final VoidCallback onView;
  final VoidCallback onDelete;

  const _ProductCard({required this.product, required this.isDark, required this.onView, required this.onDelete});

  @override
  State<_ProductCard> createState() => _ProductCardState();
}

class _ProductCardState extends State<_ProductCard> {
  bool _isHovering = false;

  String _formatDate(DateTime date) {
    final d = date.toLocal();
    const months = ['', 'jan', 'feb', 'mar', 'apr', 'maj', 'jun', 'jul', 'aug', 'sep', 'okt', 'nov', 'dec'];
    return '${d.day}. ${months[d.month]} ${d.year}.';
  }

  @override
  Widget build(BuildContext context) {
    final isDark = widget.isDark;
    final product = widget.product;
    final primary = isDark ? AppColors.secondary : AppColors.primary;
    final textPrimary = isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary;
    final textTertiary = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final statusColor = product.isPublished ? AppColors.success : AppColors.warning;
    final statusColorDark = product.isPublished ? AppColors.successDark : AppColors.warningDark;
    final coverImage = product.images.isNotEmpty ? product.images.first.url : null;

    return MouseRegion(
      onEnter: (_) => setState(() => _isHovering = true),
      onExit: (_) => setState(() => _isHovering = false),
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 200),
        decoration: BoxDecoration(
          color: isDark ? AppColors.darkSurface : Colors.white,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: _isHovering ? primary : (isDark ? AppColors.darkBorder : AppColors.lightBorder)),
          boxShadow: _isHovering
              ? [BoxShadow(color: Colors.black.withValues(alpha: isDark ? 0.3 : 0.08), blurRadius: 10, offset: const Offset(0, 4))]
              : null,
        ),
        clipBehavior: Clip.antiAlias,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Cover thumbnail (or a plain icon tile when the product has no images
            // yet) — the single biggest "does this card look empty" fix: a real
            // photo makes the grid instantly scannable instead of icon soup.
            SizedBox(
              height: 140,
              width: double.infinity,
              child: coverImage != null
                  ? Image.network(
                      coverImage,
                      fit: BoxFit.cover,
                      errorBuilder: (context, error, stackTrace) => _CoverPlaceholder(primary: primary),
                    )
                  : _CoverPlaceholder(primary: primary),
            ),
            Expanded(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(14, 10, 14, 12),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      product.name,
                      style: TextStyle(fontSize: 15, fontWeight: FontWeight.w700, color: textPrimary),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                    const SizedBox(height: 4),
                    Row(
                      children: [
                        Icon(LucideIcons.mapPin, size: 12, color: textTertiary),
                        const SizedBox(width: 4),
                        Expanded(
                          child: Text(
                            product.city.label,
                            style: TextStyle(fontSize: 12, color: textTertiary),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 2),
                    Row(
                      children: [
                        Icon(
                          product.date != null ? LucideIcons.calendarDays : LucideIcons.repeat,
                          size: 12,
                          color: textTertiary,
                        ),
                        const SizedBox(width: 4),
                        Expanded(
                          child: Text(
                            product.date != null ? _formatDate(product.date!) : product.ticketingMode.label,
                            style: TextStyle(fontSize: 12, color: textTertiary),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 8),
                    Wrap(
                      spacing: 6,
                      runSpacing: 6,
                      children: [
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                          decoration: BoxDecoration(
                            color: primary.withValues(alpha: isDark ? 0.18 : 0.1),
                            borderRadius: BorderRadius.circular(20),
                          ),
                          child: Text(product.categoryName,
                              style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: primary),
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis),
                        ),
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                          decoration: BoxDecoration(
                            color: statusColor.withValues(alpha: isDark ? 0.18 : 0.1),
                            borderRadius: BorderRadius.circular(20),
                          ),
                          child: Text(product.isPublished ? 'Objavljen' : 'Nacrt',
                              style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: statusColorDark)),
                        ),
                      ],
                    ),
                    const Spacer(),
                    Row(
                      children: [
                        Expanded(
                          child: _CardActionButton(
                            icon: LucideIcons.eye,
                            label: 'Pregled',
                            isDestructive: false,
                            onTap: widget.onView,
                          ),
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: _CardActionButton(
                            icon: LucideIcons.trash2,
                            label: 'Obriši',
                            isDestructive: true,
                            onTap: widget.onDelete,
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Mirrors OrganizationsScreen's `_ActionButton` — same visual language (10%-alpha
/// tinted background, 20%-alpha border, 12px radius) so every card-grid screen's
/// action buttons look like one system.
class _CardActionButton extends StatelessWidget {
  final IconData icon;
  final String label;
  final bool isDestructive;
  final VoidCallback onTap;

  const _CardActionButton({required this.icon, required this.label, required this.isDestructive, required this.onTap});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primary = isDark ? AppColors.secondary : AppColors.primary;
    final fg = isDestructive ? AppColors.error : primary;
    final bg = isDestructive
        ? AppColors.error.withValues(alpha: isDark ? 0.16 : 0.08)
        : primary.withValues(alpha: isDark ? 0.18 : 0.1);

    return Material(
      color: bg,
      borderRadius: BorderRadius.circular(12),
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(vertical: 9, horizontal: 10),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: fg.withValues(alpha: 0.2)),
          ),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(icon, size: 16, color: fg),
              const SizedBox(width: 6),
              Flexible(
                child: Text(
                  label,
                  style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: fg),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _CoverPlaceholder extends StatelessWidget {
  final Color primary;

  const _CoverPlaceholder({required this.primary});

  @override
  Widget build(BuildContext context) {
    return Container(
      color: primary.withValues(alpha: 0.12),
      alignment: Alignment.center,
      child: Icon(LucideIcons.image, color: primary, size: 26),
    );
  }
}
