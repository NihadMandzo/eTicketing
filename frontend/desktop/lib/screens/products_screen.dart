import 'dart:async';

import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../models/responses/product_response.dart';
import '../models/search_objects/product_search_object.dart';
import '../providers/product_provider.dart';
import '../theme/app_colors.dart';
import 'product_detail_screen.dart';
import 'widgets/pagination_bar.dart';
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

  List<ProductResponse> _products = [];
  bool _isLoading = true;

  int _currentPage = 0;
  int _totalCount = 0;
  int _pageSize = 10;

  int get _totalPages => (_totalCount / _pageSize).ceil().clamp(1, 99999);

  bool get _isDark => Theme.of(context).brightness == Brightness.dark;

  @override
  void initState() {
    super.initState();
    _loadData();
  }

  void _onSearchChanged(String _) {
    if (_debounce?.isActive ?? false) _debounce!.cancel();
    _debounce = Timer(const Duration(milliseconds: 300), () {
      setState(() => _currentPage = 0);
      _loadData();
    });
  }

  Future<void> _loadData() async {
    setState(() => _isLoading = true);
    try {
      final searchObject = ProductSearchObject(
        page: _currentPage,
        pageSize: _pageSize,
        fts: _searchController.text.trim().isEmpty ? null : _searchController.text.trim(),
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
      builder: (_) => ProductUpsertDialog(onSaved: _loadData),
    );
  }

  Future<void> _openDetail(ProductResponse product) async {
    await Navigator.of(context).push(
      MaterialPageRoute(builder: (_) => ProductDetailScreen(product: product)),
    );
    // The detail screen may have edited/published/deleted the product.
    if (mounted) _loadData();
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
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final isDark = _isDark;
    final primary = isDark ? AppColors.secondary : AppColors.primary;
    final primaryDark = isDark ? AppColors.primary : AppColors.primaryDark;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Column(
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

          // ── Search ──
          Container(
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
                hintStyle:
                    TextStyle(color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary),
                prefixIcon: Icon(LucideIcons.search,
                    color: isDark ? AppColors.darkTextDisabled : AppColors.lightTextDisabled, size: 18),
                border: InputBorder.none,
                contentPadding: const EdgeInsets.symmetric(vertical: 12),
              ),
              style: TextStyle(color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary),
            ),
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
                mainAxisExtent: 220,
                crossAxisSpacing: 14,
                mainAxisSpacing: 14,
              ),
              itemCount: _products.length,
              itemBuilder: (context, index) {
                final product = _products[index];
                return _ProductCard(
                  product: product,
                  isDark: isDark,
                  onTap: () => _openDetail(product),
                );
              },
            ),

          Padding(
            padding: const EdgeInsets.only(top: 16),
            child: PaginationBar(
              currentPage: _currentPage,
              totalPages: _totalPages,
              onPageChanged: _goToPage,
              pageSize: _pageSize,
              onPageSizeChanged: _onPageSizeChanged,
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
  final VoidCallback onTap;

  const _ProductCard({required this.product, required this.isDark, required this.onTap});

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

    return MouseRegion(
      onEnter: (_) => setState(() => _isHovering = true),
      onExit: (_) => setState(() => _isHovering = false),
      cursor: SystemMouseCursors.click,
      child: GestureDetector(
        onTap: widget.onTap,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 200),
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: isDark ? AppColors.darkSurface : Colors.white,
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: _isHovering ? primary : (isDark ? AppColors.darkBorder : AppColors.lightBorder)),
            boxShadow: _isHovering
                ? [BoxShadow(color: Colors.black.withValues(alpha: isDark ? 0.3 : 0.08), blurRadius: 10, offset: const Offset(0, 4))]
                : null,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(
                width: 44,
                height: 44,
                decoration: BoxDecoration(
                  color: primary.withValues(alpha: isDark ? 0.18 : 0.1),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Icon(LucideIcons.package, color: primary, size: 22),
              ),
              const SizedBox(height: 12),
              Text(
                product.name,
                style: TextStyle(fontSize: 15, fontWeight: FontWeight.w700, color: textPrimary),
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ),
              const SizedBox(height: 4),
              Text(
                product.date != null ? _formatDate(product.date!) : product.ticketingMode.label,
                style: TextStyle(fontSize: 12, color: textTertiary),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
              const Spacer(),
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
            ],
          ),
        ),
      ),
    );
  }
}
