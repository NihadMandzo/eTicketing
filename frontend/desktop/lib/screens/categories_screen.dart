import 'dart:async';

import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import '../models/responses/category_response.dart';
import '../models/search_objects/base_search_object.dart';
import '../providers/category_provider.dart';
import '../theme/app_colors.dart';
import 'widgets/pagination_bar.dart';
import 'widgets/category_upsert_dialog.dart';
import '../main.dart';

class CategoriesScreen extends StatefulWidget {
  const CategoriesScreen({super.key});

  @override
  State<CategoriesScreen> createState() => _CategoriesScreenState();
}

class _CategoriesScreenState extends State<CategoriesScreen> {
  final _searchController = TextEditingController();
  final CategoryProvider _provider = CategoryProvider();
  Timer? _debounce;

  List<CategoryResponse> _categories = [];
  bool _isLoading = true;

  int _currentPage = 0;
  int _totalCount = 0;
  static const int _pageSize = 8;

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
      final searchObject = BaseSearchObject(
        page: _currentPage,
        pageSize: _pageSize,
        fts: _searchController.text.trim().isEmpty
            ? null
            : _searchController.text.trim(),
      );
      final result = await _provider.getAll(
        searchObject: searchObject,
        fromJson: CategoryResponse.fromJson,
      );
      if (mounted) {
        setState(() {
          _categories = result.items;
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

  Future<void> _deleteCategory(CategoryResponse category) async {
    final isDark = _isDark;
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        backgroundColor: isDark ? AppColors.darkSurface : Colors.white,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        title: Text('Obriši kategoriju',
            style: TextStyle(
                fontWeight: FontWeight.w700,
                color: isDark
                    ? AppColors.darkTextPrimary
                    : AppColors.lightTextPrimary)),
        content: Text(
            'Da li ste sigurni da želite obrisati kategoriju "${category.name}"?',
            style: TextStyle(
                color: isDark
                    ? AppColors.darkTextSecondary
                    : AppColors.lightTextSecondary)),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(false),
            child: Text('Odustani',
                style: TextStyle(
                    color: isDark
                        ? AppColors.darkTextTertiary
                        : AppColors.lightTextTertiary)),
          ),
          ElevatedButton(
            style: ElevatedButton.styleFrom(
              backgroundColor: AppColors.errorDark,
              foregroundColor: Colors.white,
              shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(10)),
            ),
            onPressed: () => Navigator.of(ctx).pop(true),
            child: const Text('Obriši'),
          ),
        ],
      ),
    );

    if (confirmed != true || !mounted) return;

    try {
      await _provider.delete(category.id);
      if (!mounted) return;

      // Go back a page if this was the only item on the page
      if (_categories.length == 1 && _currentPage > 0) {
        setState(() => _currentPage--);
      }
      await _loadData();

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Row(
              children: [
                const Icon(Icons.check_circle_outline, color: Colors.white),
                const SizedBox(width: 10),
                Text('Kategorija "${category.name}" je uspješno obrisana'),
              ],
            ),
            backgroundColor: AppColors.primary,
            behavior: SnackBarBehavior.floating,
            shape:
                RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
            margin: const EdgeInsets.all(16),
            duration: const Duration(seconds: 3),
          ),
        );
      }
    } catch (e) {
      if (mounted) handleApiError(e);
    }
  }

  void _openDialog({CategoryResponse? category}) {
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => CategoryUpsertDialog(
        category: category,
        onSaved: _loadData,
      ),
    );
  }

  void _goToPage(int page) {
    if (page < 0 || page >= _totalPages) return;
    setState(() => _currentPage = page);
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
    return Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // ── Header ─────────────────────────────────────────────────
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Kategorije',
                      style: TextStyle(
                        fontSize: 30,
                        fontWeight: FontWeight.w700,
                        color: isDark
                            ? AppColors.darkTextPrimary
                            : AppColors.lightTextPrimary,
                        height: 1.2,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      'Upravljajte kategorijama i klasifikacijama događaja',
                      style: TextStyle(
                          fontSize: 16,
                          color: isDark
                              ? AppColors.darkTextSecondary
                              : AppColors.lightTextSecondary),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 16),
              Container(
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    colors: [primary, primaryDark],
                  ),
                  borderRadius: BorderRadius.circular(12),
                  boxShadow: [
                    BoxShadow(
                      color: primary.withValues(alpha: 0.2),
                      blurRadius: 10,
                      offset: const Offset(0, 4),
                    ),
                  ],
                ),
                child: Material(
                  color: Colors.transparent,
                  child: InkWell(
                    borderRadius: BorderRadius.circular(12),
                    onTap: () => _openDialog(),
                    child: Padding(
                      padding: const EdgeInsets.symmetric(
                          horizontal: 16, vertical: 10),
                      child: Row(
                        children: const [
                          Icon(LucideIcons.plus, color: Colors.white, size: 20),
                          SizedBox(width: 8),
                          Text(
                            'Dodaj Kategoriju',
                            style: TextStyle(
                              color: Colors.white,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
            ],
          ),

          const SizedBox(height: 24),

          // ── Search ────────────────────────────────────────────────
          Container(
            height: 44,
            decoration: BoxDecoration(
              color: isDark ? AppColors.darkSurface : Colors.white,
              border: Border.all(
                  color: isDark
                      ? AppColors.darkBorderInput
                      : AppColors.lightBorderInput),
              borderRadius: BorderRadius.circular(12),
            ),
            child: TextField(
              controller: _searchController,
              onChanged: _onSearchChanged,
              decoration: InputDecoration(
                hintText: 'Pretražite kategorije...',
                hintStyle: TextStyle(
                    color: isDark
                        ? AppColors.darkTextTertiary
                        : AppColors.lightTextTertiary),
                prefixIcon: Icon(LucideIcons.search,
                    color: isDark
                        ? AppColors.darkTextDisabled
                        : AppColors.lightTextDisabled,
                    size: 18),
                border: InputBorder.none,
                contentPadding: const EdgeInsets.symmetric(vertical: 12),
              ),
              style: TextStyle(
                  color: isDark
                      ? AppColors.darkTextPrimary
                      : AppColors.lightTextPrimary),
            ),
          ),

          const SizedBox(height: 12),

          // ── Count ─────────────────────────────────────────────────
          if (!_isLoading)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Text(
                '$_totalCount kategorija',
                style: TextStyle(
                    fontSize: 13,
                    color: isDark
                        ? AppColors.darkTextTertiary
                        : AppColors.lightTextTertiary),
              ),
            ),

          // ── Grid ──────────────────────────────────────────────────
          Expanded(
            child: _isLoading
                ? Center(
                    child: CircularProgressIndicator(color: primary),
                  )
                : _categories.isEmpty
                    ? Center(
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Icon(LucideIcons.layoutGrid,
                                size: 48,
                                color: isDark
                                    ? AppColors.darkBorderInput
                                    : AppColors.lightBorderInput),
                            const SizedBox(height: 12),
                            Text(
                              'Nema kategorija',
                              style: TextStyle(
                                  color: isDark
                                      ? AppColors.darkTextTertiary
                                      : AppColors.lightTextTertiary,
                                  fontSize: 16),
                            ),
                          ],
                        ),
                      )
                    : LayoutBuilder(
                        builder: (context, constraints) {
                          int crossAxisCount = 4;
                          if (constraints.maxWidth >= 1400) {
                            crossAxisCount = 8;
                          } else if (constraints.maxWidth >= 1100) {
                            crossAxisCount = 6;
                          } else if (constraints.maxWidth >= 800) {
                            crossAxisCount = 5;
                          }

                          return GridView.builder(
                            gridDelegate:
                                SliverGridDelegateWithFixedCrossAxisCount(
                              crossAxisCount: crossAxisCount,
                              crossAxisSpacing: 12,
                              mainAxisSpacing: 12,
                              childAspectRatio: 0.9,
                            ),
                            itemCount: _categories.length,
                            itemBuilder: (context, index) {
                              final cat = _categories[index];
                              return _CategoryCard(
                                category: cat,
                                onEdit: () => _openDialog(category: cat),
                                onDelete: () => _deleteCategory(cat),
                              );
                            },
                          );
                        },
                      ),
          ),

          // ── Pagination ───────────────────────────────────────────
          Padding(
            padding: const EdgeInsets.only(top: 16),
            child: PaginationBar(
              currentPage: _currentPage,
              totalPages: _totalPages,
              onPageChanged: _goToPage,
            ),
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────────
//  Category Card
// ─────────────────────────────────────────────────────────────
class _CategoryCard extends StatefulWidget {
  final CategoryResponse category;
  final VoidCallback onEdit;
  final VoidCallback onDelete;

  const _CategoryCard({
    required this.category,
    required this.onEdit,
    required this.onDelete,
  });

  @override
  State<_CategoryCard> createState() => _CategoryCardState();
}

class _CategoryCardState extends State<_CategoryCard> {
  bool _isHovering = false;

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primary = isDark ? AppColors.secondary : AppColors.primary;

    return MouseRegion(
      onEnter: (_) => setState(() => _isHovering = true),
      onExit: (_) => setState(() => _isHovering = false),
      cursor: SystemMouseCursors.click,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 200),
        decoration: BoxDecoration(
          color: isDark ? AppColors.darkSurface : Colors.white,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(
            color: _isHovering
                ? primary
                : (isDark ? AppColors.darkBorder : AppColors.lightBorder),
          ),
          boxShadow: _isHovering
              ? [
                  BoxShadow(
                    color: Colors.black.withValues(alpha: isDark ? 0.3 : 0.08),
                    blurRadius: 10,
                    offset: const Offset(0, 4),
                  ),
                ]
              : null,
        ),
        child: Stack(
          children: [
            // ── Card content ──
            Positioned.fill(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(16, 20, 16, 16),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  crossAxisAlignment: CrossAxisAlignment.center,
                  children: [
                  // Centered Image
                  ClipRRect(
                    borderRadius: BorderRadius.circular(14),
                    child: widget.category.iconUrl.isNotEmpty
                        ? Image.network(
                            widget.category.iconUrl,
                            width: 80,
                            height: 80,
                            fit: BoxFit.cover,
                            errorBuilder: (context, error, stackTrace) =>
                                Container(
                                  width: 80,
                                  height: 80,
                                  color: isDark
                                      ? AppColors.darkSurfaceMuted
                                      : AppColors.lightSurfaceMuted,
                                  child: Icon(LucideIcons.imageOff,
                                      color: isDark
                                          ? AppColors.darkTextDisabled
                                          : AppColors.lightTextDisabled,
                                      size: 36),
                                ),
                          )
                        : Container(
                            width: 80,
                            height: 80,
                            color: isDark
                                ? AppColors.darkSurfaceMuted
                                : AppColors.lightSurfaceMuted,
                            child: Icon(LucideIcons.image,
                                color: isDark
                                    ? AppColors.darkTextDisabled
                                    : AppColors.lightTextDisabled,
                                size: 36),
                          ),
                  ),
                  const SizedBox(height: 14),

                  // Name
                  Text(
                    widget.category.name,
                    style: TextStyle(
                      fontSize: 18,
                      fontWeight: FontWeight.w700,
                      color: isDark
                          ? AppColors.darkTextPrimary
                          : AppColors.lightTextPrimary,
                    ),
                    textAlign: TextAlign.center,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                  const SizedBox(height: 6),

                  // Description
                  Text(
                    widget.category.description,
                    style: TextStyle(
                      fontSize: 13,
                      color: isDark
                          ? AppColors.darkTextTertiary
                          : AppColors.lightTextTertiary,
                      height: 1.4,
                    ),
                    textAlign: TextAlign.center,
                    maxLines: 3,
                    overflow: TextOverflow.ellipsis,
                  ),
                ],
              ),
            ),
            ),

            // ── Hover action buttons (top-right) ──
            Positioned(
              top: 8,
              right: 8,
              child: AnimatedOpacity(
                opacity: _isHovering ? 1.0 : 0.0,
                duration: const Duration(milliseconds: 200),
                child: IgnorePointer(
                  ignoring: !_isHovering,
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      _ActionBtn(
                        icon: LucideIcons.pencil,
                        onTap: widget.onEdit,
                      ),
                      const SizedBox(width: 4),
                      _ActionBtn(
                        icon: LucideIcons.trash2,
                        onTap: widget.onDelete,
                        isDestructive: true,
                      ),
                    ],
                  ),
                ),
              ),
            )
          ],
        ),
      ),
    );
  }
}

class _ActionBtn extends StatelessWidget {
  final IconData icon;
  final VoidCallback onTap;
  final bool isDestructive;

  const _ActionBtn({
    required this.icon,
    required this.onTap,
    this.isDestructive = false,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Material(
      color: isDark ? AppColors.darkSurface : Colors.white,
      borderRadius: BorderRadius.circular(7),
      child: InkWell(
        borderRadius: BorderRadius.circular(7),
        onTap: onTap,
        hoverColor: isDestructive
            ? (isDark ? AppColors.errorBgDarkMode : AppColors.errorBg)
            : (isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted),
        child: Padding(
          padding: const EdgeInsets.all(5),
          child: Icon(
            icon,
            size: 14,
            color: isDestructive
                ? AppColors.error
                : (isDark
                    ? AppColors.darkTextTertiary
                    : AppColors.lightTextTertiary),
          ),
        ),
      ),
    );
  }
}
