import 'package:flutter/material.dart';

import '../../theme/app_colors.dart';

/// Multiselect role filter for the SuperAdmin-facing platform Users screen —
/// a `Wrap` of toggleable `FilterChip`s, not an overlay/dropdown. Deliberately
/// simpler than CategoryMultiSelectFilter's CompositedTransformTarget/Overlay
/// pattern: there are only 4 fixed, always-available options here (no async
/// loading), and a `Wrap` can never overflow horizontally the way a fixed-
/// width dropdown trigger can, so this sidesteps that whole class of bug for
/// a filter this simple.
///
/// [selectedRoles] holds RoleType enum member names ('SuperAdmin', 'Admin',
/// 'OrganizationSuperAdmin', 'OrganizationAdmin'); empty means "every role"
/// (AdminQuery.RoleFilters' default, no filter).
class RoleMultiSelectFilter extends StatelessWidget {
  final List<String> selectedRoles;
  final ValueChanged<List<String>> onChanged;

  const RoleMultiSelectFilter({super.key, required this.selectedRoles, required this.onChanged});

  static const Map<String, String> _options = {
    'SuperAdmin': 'Super Admin',
    'Admin': 'Admin',
    'OrganizationSuperAdmin': 'Org Super Admin',
    'OrganizationAdmin': 'Org Admin',
  };

  void _toggle(String role) {
    final updated = List<String>.from(selectedRoles);
    if (updated.contains(role)) {
      updated.remove(role);
    } else {
      updated.add(role);
    }
    onChanged(updated);
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final primary = isDark ? AppColors.secondary : AppColors.primary;
    final borderColor = isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput;
    final textSecondary = isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary;

    return Wrap(
      spacing: 8,
      runSpacing: 8,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: _options.entries.map((e) {
        final isSelected = selectedRoles.contains(e.key);
        return FilterChip(
          label: Text(e.value),
          selected: isSelected,
          onSelected: (_) => _toggle(e.key),
          showCheckmark: false,
          labelStyle: TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w600,
            color: isSelected ? (isDark ? AppColors.darkBackground : Colors.white) : textSecondary,
          ),
          backgroundColor: isDark ? AppColors.darkSurface : Colors.white,
          selectedColor: primary,
          side: BorderSide(color: isSelected ? primary : borderColor),
          padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 2),
        );
      }).toList(),
    );
  }
}
