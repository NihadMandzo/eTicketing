import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

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
              ? const Padding(
                  padding: EdgeInsets.symmetric(horizontal: 4),
                  child: Text('…', style: TextStyle(color: Color(0xFF6B7280))),
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
    return Material(
      color: Colors.white,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(8),
        side: const BorderSide(color: Color(0xFFE5E7EB)),
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
                ? const Color(0xFF374151)
                : const Color(0xFFD1D5DB),
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
    return Material(
      color: isActive ? const Color(0xFF0D7C66) : Colors.white,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(8),
        side: BorderSide(
          color: isActive ? const Color(0xFF0D7C66) : const Color(0xFFE5E7EB),
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
                color: isActive ? Colors.white : const Color(0xFF374151),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
