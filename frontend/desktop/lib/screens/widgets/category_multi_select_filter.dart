import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../models/responses/category_response.dart';
import '../../models/search_objects/base_search_object.dart';
import '../../providers/category_provider.dart';
import '../../theme/app_colors.dart';

/// A category checkbox-multiselect filter dropdown, used on the
/// organizations list. No multiselect widget existed anywhere in this app
/// to reuse — modeled on app_header.dart's `CompositedTransformTarget`/
/// `LayerLink` + `OverlayEntry` + `CompositedTransformFollower` dropdown
/// pattern (the one existing overlay-dropdown in the codebase), rather than
/// `showMenu`, so behavior (outside-tap-to-close, positioning) matches.
class CategoryMultiSelectFilter extends StatefulWidget {
  final List<int> selectedCategoryIds;
  final ValueChanged<List<int>> onChanged;

  const CategoryMultiSelectFilter({
    super.key,
    required this.selectedCategoryIds,
    required this.onChanged,
  });

  @override
  State<CategoryMultiSelectFilter> createState() => _CategoryMultiSelectFilterState();
}

class _CategoryMultiSelectFilterState extends State<CategoryMultiSelectFilter> {
  final _layerLink = LayerLink();
  OverlayEntry? _overlay;
  bool _menuOpen = false;

  List<CategoryResponse> _categories = [];
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _loadCategories();
  }

  Future<void> _loadCategories() async {
    try {
      // 100 is the server's enforced ceiling (BaseSearchObjectValidator caps
      // PageSize at 1-100) — page through all results rather than assuming
      // one page is "all categories", so a catalog that grows past 100
      // entries doesn't silently lose entries from this filter.
      final provider = CategoryProvider();
      final allCategories = <CategoryResponse>[];
      var page = 0;
      const pageSize = 100;
      while (true) {
        final result = await provider.getAll(
          searchObject: BaseSearchObject(page: page, pageSize: pageSize),
          fromJson: CategoryResponse.fromJson,
        );
        allCategories.addAll(result.items);
        if (result.items.isEmpty || allCategories.length >= result.totalCount) break;
        page++;
      }

      if (mounted) {
        setState(() {
          _categories = allCategories;
          _isLoading = false;
        });
        // setState rebuilds this widget's element, but the dropdown content
        // is rendered by a separate OverlayEntry — if it's open, it needs to
        // be told to rebuild too or it stays on the spinner.
        _scheduleOverlayRebuild();
      }
    } catch (_) {
      if (mounted) {
        setState(() => _isLoading = false);
        _scheduleOverlayRebuild();
      }
    }
  }

  /// Defers `_overlay?.markNeedsBuild()` to just after the current frame
  /// instead of calling it inline. Calling it inline from `didUpdateWidget`
  /// (see below) throws "setState() or markNeedsBuild() called during
  /// build": `didUpdateWidget` runs while the framework's BuildOwner still
  /// holds the build-phase lock for the *whole* tree (not just this
  /// widget), and marking a different element (the OverlayEntry's) dirty
  /// during that window trips the same assertion setState() would.
  /// Post-frame callbacks run after that lock is released, so this is safe
  /// from every call site, not just didUpdateWidget's.
  void _scheduleOverlayRebuild() {
    if (_overlay == null) return;
    WidgetsBinding.instance.addPostFrameCallback((_) => _overlay?.markNeedsBuild());
  }

  void _toggleCategory(int id) {
    final updated = List<int>.from(widget.selectedCategoryIds);
    if (updated.contains(id)) {
      updated.remove(id);
    } else {
      updated.add(id);
    }
    widget.onChanged(updated);
  }

  // ── Overlay lifecycle (mirrors app_header.dart's _AppHeaderState) ──────────

  void _openMenu() {
    if (_menuOpen) return;
    setState(() => _menuOpen = true);
    _overlay = OverlayEntry(builder: (_) => _buildOverlay());
    Overlay.of(context).insert(_overlay!);
  }

  void _closeMenu() {
    if (!_menuOpen) return;
    setState(() => _menuOpen = false);
    _overlay?.remove();
    _overlay = null;
  }

  void _toggleMenu() => _menuOpen ? _closeMenu() : _openMenu();

  @override
  void didUpdateWidget(covariant CategoryMultiSelectFilter oldWidget) {
    super.didUpdateWidget(oldWidget);
    // The checked state is rendered by the OverlayEntry, not by this
    // widget's own build() — after onChanged rebuilds the parent with a new
    // selectedCategoryIds, the open menu needs to be told to rebuild too, or
    // it keeps showing the pre-click checkbox values. Deferred — see
    // _scheduleOverlayRebuild's doc comment for why calling this inline
    // here crashed the app.
    _scheduleOverlayRebuild();
  }

  @override
  void dispose() {
    _overlay?.remove();
    super.dispose();
  }

  Widget _buildOverlay() {
    return Stack(
      children: [
        Positioned.fill(
          child: GestureDetector(
            behavior: HitTestBehavior.translucent,
            onTap: _closeMenu,
          ),
        ),
        CompositedTransformFollower(
          link: _layerLink,
          showWhenUnlinked: false,
          targetAnchor: Alignment.bottomLeft,
          followerAnchor: Alignment.topLeft,
          offset: const Offset(0, 8),
          child: Material(
            color: Colors.transparent,
            child: _CategoryDropdownCard(
              categories: _categories,
              isLoading: _isLoading,
              selectedCategoryIds: widget.selectedCategoryIds,
              onToggle: _toggleCategory,
            ),
          ),
        ),
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final selectedCount = widget.selectedCategoryIds.length;
    return CompositedTransformTarget(
      link: _layerLink,
      child: GestureDetector(
        onTap: _toggleMenu,
        child: Container(
          height: 44,
          padding: const EdgeInsets.symmetric(horizontal: 14),
          decoration: BoxDecoration(
            color: isDark ? AppColors.darkSurface : Colors.white,
            border: Border.all(
              color: selectedCount > 0
                  ? (isDark ? AppColors.secondary : AppColors.primary)
                  : (isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
            ),
            borderRadius: BorderRadius.circular(12),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(LucideIcons.tag,
                  size: 16,
                  color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary),
              const SizedBox(width: 8),
              Text(
                selectedCount == 0 ? 'Sve kategorije' : '$selectedCount kategorija',
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w500,
                  color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
                ),
              ),
              const SizedBox(width: 6),
              AnimatedRotation(
                turns: _menuOpen ? 0.5 : 0,
                duration: const Duration(milliseconds: 200),
                child: Icon(Icons.keyboard_arrow_down_rounded,
                    size: 18,
                    color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

// ─── Dropdown card ──────────────────────────────────────────────────────────

class _CategoryDropdownCard extends StatelessWidget {
  final List<CategoryResponse> categories;
  final bool isLoading;
  final List<int> selectedCategoryIds;
  final ValueChanged<int> onToggle;

  const _CategoryDropdownCard({
    required this.categories,
    required this.isLoading,
    required this.selectedCategoryIds,
    required this.onToggle,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final borderColor = isDark ? AppColors.darkBorder : AppColors.lightBorder;
    return Container(
      width: 260,
      constraints: const BoxConstraints(maxHeight: 320),
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurface : Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: borderColor),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: isDark ? 0.4 : 0.12),
            blurRadius: 28,
            offset: const Offset(0, 8),
          ),
        ],
      ),
      child: isLoading
          ? const Padding(
              padding: EdgeInsets.all(20),
              child: Center(
                child: SizedBox(
                  width: 20,
                  height: 20,
                  child: CircularProgressIndicator(strokeWidth: 2),
                ),
              ),
            )
          : categories.isEmpty
              ? Padding(
                  padding: const EdgeInsets.all(16),
                  child: Text(
                    'Nema kategorija',
                    style: TextStyle(
                      color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary,
                    ),
                  ),
                )
              : ListView(
                  shrinkWrap: true,
                  padding: const EdgeInsets.symmetric(vertical: 6),
                  children: categories.map((category) {
                    final isSelected = selectedCategoryIds.contains(category.id);
                    return CheckboxListTile(
                      value: isSelected,
                      onChanged: (_) => onToggle(category.id),
                      controlAffinity: ListTileControlAffinity.leading,
                      dense: true,
                      activeColor: isDark ? AppColors.secondary : AppColors.primary,
                      title: Text(
                        category.name,
                        style: TextStyle(
                          fontSize: 14,
                          color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
                        ),
                      ),
                    );
                  }).toList(),
                ),
    );
  }
}
