import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../theme/app_colors.dart';

/// A reusable pagination bar.
///
/// [currentPage] is 0-indexed. [totalPages] is the total number of pages.
/// [onPageChanged] is called with the new 0-indexed page number.
class PaginationBar extends StatelessWidget {
  final int currentPage;
  final int totalPages;
  final void Function(int page) onPageChanged;

  const PaginationBar({
    super.key,
    required this.currentPage,
    required this.totalPages,
    required this.onPageChanged,
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
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final pages = _buildPageNumbers();

    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
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
                          color: isDark
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
