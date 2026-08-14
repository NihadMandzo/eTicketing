import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../theme/app_colors.dart';

/// A reusable pagination bar, with an optional page-size selector.
///
/// [currentPage] is 0-indexed. [totalPages] is the total number of pages.
/// [onPageChanged] is called with the new 0-indexed page number.
///
/// When [pageSize]/[onPageSizeChanged] are both supplied, a "Po stranici: N"
/// dropdown is rendered alongside the pager — every card-grid screen in the
/// app (Categories, Organizations, Users, organization detail's Events/Users
/// tabs) uses this so the number of cards fetched per page — and therefore
/// how many rows the grid renders — is user-controllable, not hardcoded.
/// Wrapped in a `Wrap` (not a `Row`) so the selector drops to its own line
/// rather than overflowing at narrow widths, and the whole bar is centered
/// (not left-aligned) regardless of how wide the parent column is.
///
/// [pageSizeOptions] defaults to the one fixed set of page sizes used
/// everywhere in the app (5/10/20/50) — every call site should rely on this
/// default rather than passing its own list, so the selector's choices are
/// identical on every paginated screen.
class PaginationBar extends StatelessWidget {
  final int currentPage;
  final int totalPages;
  final void Function(int page) onPageChanged;
  final int? pageSize;
  final List<int> pageSizeOptions;
  final ValueChanged<int>? onPageSizeChanged;

  const PaginationBar({
    super.key,
    required this.currentPage,
    required this.totalPages,
    required this.onPageChanged,
    this.pageSize,
    this.pageSizeOptions = const [5, 10, 20, 50],
    this.onPageSizeChanged,
  });

  List<int?> _buildPageNumbers() {
    if (totalPages <= 7) return List.generate(totalPages, (i) => i);

    final pages = <int?>[];
    pages.add(0);
    if (currentPage > 2) pages.add(null); // leading ellipsis
    for (int i = (currentPage - 1).clamp(1, totalPages - 2);
        i <= (currentPage + 1).clamp(1, totalPages - 2);
        i++) {
      pages.add(i);
    }
    if (currentPage < totalPages - 3) pages.add(null); // trailing ellipsis
    pages.add(totalPages - 1);
    return pages;
  }

  @override
  Widget build(BuildContext context) {
    final pages = _buildPageNumbers();
    final showPageSize = pageSize != null && onPageSizeChanged != null;

    final pager = Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        _NavBtn(
          icon: LucideIcons.chevronLeft,
          onTap: currentPage > 0 ? () => onPageChanged(currentPage - 1) : null,
        ),
        const SizedBox(width: 6),
        for (final page in pages)
          page == null
              ? Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 4),
                  child: Text('…',
                      style: TextStyle(
                          color: Theme.of(context).brightness == Brightness.dark
                              ? AppColors.darkTextTertiary
                              : AppColors.lightTextTertiary)),
                )
              : Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 2),
                  child: _PageBtn(
                    label: '${page + 1}',
                    isActive: page == currentPage,
                    onTap: () => onPageChanged(page),
                  ),
                ),
        const SizedBox(width: 6),
        _NavBtn(
          icon: LucideIcons.chevronRight,
          onTap: currentPage < totalPages - 1
              ? () => onPageChanged(currentPage + 1)
              : null,
        ),
      ],
    );

    if (!showPageSize) {
      return Center(child: pager);
    }

    // Center wraps the Wrap so the whole bar sits in the middle of whatever
    // width its parent (typically a full-width Column) gives it, instead of
    // hugging the left edge the way an unwrapped Wrap (sized to its content)
    // otherwise would.
    return Center(
      child: Wrap(
        alignment: WrapAlignment.center,
        crossAxisAlignment: WrapCrossAlignment.center,
        spacing: 20,
        runSpacing: 10,
        children: [
          pager,
          _PageSizeSelector(
            value: pageSize!,
            options: pageSizeOptions,
            onChanged: onPageSizeChanged!,
          ),
        ],
      ),
    );
  }
}

class _PageSizeSelector extends StatelessWidget {
  final int value;
  final List<int> options;
  final ValueChanged<int> onChanged;

  const _PageSizeSelector({required this.value, required this.options, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final textTertiary = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final borderColor = isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput;

    // The value being displayed might not be one of the fixed options (e.g. a
    // future caller passes a custom default) — fall back to just adding it so
    // DropdownButton never asserts on an out-of-list value.
    //
    // NOTE: the ternary must be parenthesized before `..sort()` — the cascade
    // operator binds *looser* than `?:`, so `a ? b : c..sort()` actually means
    // `(a ? b : c)..sort()`, applying sort() to whichever branch is chosen —
    // including `options` itself, which is a `const` (unmodifiable) list when
    // the branch not being sorted is taken. That previously threw
    // "Unsupported operation: Cannot modify an unmodifiable list" on every
    // render where `value` was already one of the default options.
    final items = options.contains(value) ? options : ([...options, value]..sort());

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text('Po stranici:', style: TextStyle(fontSize: 13, color: textTertiary)),
        const SizedBox(width: 8),
        Container(
          height: 34,
          padding: const EdgeInsets.symmetric(horizontal: 10),
          decoration: BoxDecoration(
            color: isDark ? AppColors.darkSurface : Colors.white,
            borderRadius: BorderRadius.circular(8),
            border: Border.all(color: borderColor),
          ),
          child: DropdownButtonHideUnderline(
            child: DropdownButton<int>(
              value: value,
              isDense: true,
              dropdownColor: isDark ? AppColors.darkSurface : Colors.white,
              style: TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w600,
                  color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary),
              icon: Icon(LucideIcons.chevronDown, size: 14, color: textTertiary),
              items: items
                  .map((n) => DropdownMenuItem(value: n, child: Text('$n')))
                  .toList(),
              onChanged: (n) {
                if (n != null) onChanged(n);
              },
            ),
          ),
        ),
      ],
    );
  }
}

class _NavBtn extends StatelessWidget {
  final IconData icon;
  final VoidCallback? onTap;

  const _NavBtn({required this.icon, this.onTap});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Material(
      color: isDark ? AppColors.darkSurface : Colors.white,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(8),
        side: BorderSide(color: isDark ? AppColors.darkBorder : AppColors.lightBorder),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(8),
        onTap: onTap,
        child: SizedBox(
          width: 34,
          height: 34,
          child: Icon(
            icon,
            size: 16,
            color: onTap != null
                ? (isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary)
                : (isDark ? AppColors.darkTextDisabled : AppColors.lightBorderInput),
          ),
        ),
      ),
    );
  }
}

class _PageBtn extends StatelessWidget {
  final String label;
  final bool isActive;
  final VoidCallback onTap;

  const _PageBtn({
    required this.label,
    required this.isActive,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final activeColor = isDark ? AppColors.secondary : AppColors.primary;
    return Material(
      color: isActive ? activeColor : (isDark ? AppColors.darkSurface : Colors.white),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(8),
        side: BorderSide(
          color: isActive
              ? activeColor
              : (isDark ? AppColors.darkBorder : AppColors.lightBorder),
        ),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(8),
        onTap: onTap,
        child: SizedBox(
          width: 34,
          height: 34,
          child: Center(
            child: Text(
              label,
              style: TextStyle(
                fontSize: 13,
                fontWeight: FontWeight.w600,
                color: isActive
                    ? (isDark ? AppColors.darkBackground : Colors.white)
                    : (isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
