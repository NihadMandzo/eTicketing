import 'dart:async';

import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import '../models/responses/category_response.dart';
import '../models/search_objects/base_search_object.dart';
import '../providers/category_provider.dart';
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
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        title: const Text('Obriši kategoriju',
            style: TextStyle(fontWeight: FontWeight.w700)),
        content: Text(
            'Da li ste sigurni da želite obrisati kategoriju "${category.name}"?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(false),
            child: const Text('Odustani',
                style: TextStyle(color: Color(0xFF6B7280))),
          ),
          ElevatedButton(
            style: ElevatedButton.styleFrom(
              backgroundColor: const Color(0xFFEF4444),
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
            backgroundColor: const Color(0xFF0D7C66),
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
                  children: const [
                    Text(
                      'Kategorije',
                      style: TextStyle(
                        fontSize: 30,
                        fontWeight: FontWeight.w700,
                        color: Color(0xFF111827),
                        height: 1.2,
                      ),
                    ),
                    SizedBox(height: 8),
                    Text(
                      'Upravljajte kategorijama i klasifikacijama događaja',
                      style:
                          TextStyle(fontSize: 16, color: Color(0xFF4B5563)),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 16),
              Container(
                decoration: BoxDecoration(
                  gradient: const LinearGradient(
                    colors: [Color(0xFF0D7C66), Color(0xFF0a6b57)],
                  ),
                  borderRadius: BorderRadius.circular(12),
                  boxShadow: [
                    BoxShadow(
                      color: const Color(0xFF0D7C66).withValues(alpha: 0.2),
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
              color: Colors.white,
              border: Border.all(color: const Color(0xFFD1D5DB)),
              borderRadius: BorderRadius.circular(12),
            ),
            child: TextField(
              controller: _searchController,
              onChanged: _onSearchChanged,
              decoration: const InputDecoration(
                hintText: 'Pretražite kategorije...',
                hintStyle: TextStyle(color: Color(0xFF6B7280)),
                prefixIcon: Icon(LucideIcons.search,
                    color: Color(0xFF9CA3AF), size: 18),
                border: InputBorder.none,
                contentPadding: EdgeInsets.symmetric(vertical: 12),
              ),
              style: const TextStyle(color: Color(0xFF111827)),
            ),
          ),

          const SizedBox(height: 12),

          // ── Count ─────────────────────────────────────────────────
          if (!_isLoading)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Text(
                '$_totalCount kategorija',
                style: const TextStyle(
                    fontSize: 13, color: Color(0xFF6B7280)),
              ),
            ),

          // ── Grid ──────────────────────────────────────────────────
          Expanded(
            child: _isLoading
                ? const Center(
                    child: CircularProgressIndicator(
                        color: Color(0xFF0D7C66)),
                  )
                : _categories.isEmpty
                    ? Center(
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: const [
                            Icon(LucideIcons.layoutGrid,
                                size: 48, color: Color(0xFFD1D5DB)),
                            SizedBox(height: 12),
                            Text(
                              'Nema kategorija',
                              style: TextStyle(
                                  color: Color(0xFF6B7280), fontSize: 16),
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

    return MouseRegion(
      onEnter: (_) => setState(() => _isHovering = true),
      onExit: (_) => setState(() => _isHovering = false),
      cursor: SystemMouseCursors.click,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 200),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(
            color: _isHovering
                ? const Color(0xFF0D7C66)
                : const Color(0xFFE5E7EB),
          ),
          boxShadow: _isHovering
              ? [
                  BoxShadow(
                    color: Colors.black.withValues(alpha: 0.08),
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
                                  color: const Color(0xFFF3F4F6),
                                  child: const Icon(LucideIcons.imageOff,
                                      color: Color(0xFF9CA3AF), size: 36),
                                ),
                          )
                        : Container(
                            width: 80,
                            height: 80,
                            color: const Color(0xFFF3F4F6),
                            child: const Icon(LucideIcons.image,
                                color: Color(0xFF9CA3AF), size: 36),
                          ),
                  ),
                  const SizedBox(height: 14),

                  // Name
                  Text(
                    widget.category.name,
                    style: const TextStyle(
                      fontSize: 18,
                      fontWeight: FontWeight.w700,
                      color: Color(0xFF111827),
                    ),
                    textAlign: TextAlign.center,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                  const SizedBox(height: 6),

                  // Description
                  Text(
                    widget.category.description,
                    style: const TextStyle(
                      fontSize: 13,
                      color: Color(0xFF6B7280),
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
    return Material(
      color: Colors.white,
      borderRadius: BorderRadius.circular(7),
      child: InkWell(
        borderRadius: BorderRadius.circular(7),
        onTap: onTap,
        hoverColor: isDestructive
            ? const Color(0xFFFEE2E2)
            : const Color(0xFFF3F4F6),
        child: Padding(
          padding: const EdgeInsets.all(5),
          child: Icon(
            icon,
            size: 14,
            color: isDestructive
                ? const Color(0xFFEF4444)
                : const Color(0xFF6B7280),
          ),
        ),
      ),
    );
  }
}
