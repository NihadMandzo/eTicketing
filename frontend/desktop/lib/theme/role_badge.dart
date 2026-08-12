import 'package:flutter/material.dart';

import 'app_colors.dart';

/// Shared role-badge label/color mapping — lifted out of users_screen.dart
/// (where it was two private instance methods) so the organization detail
/// screen's admin/superadmin rows can reuse the exact same mapping.
String roleBadgeText(String role) {
  switch (role) {
    case 'SuperAdmin':
      return 'Super Admin';
    case 'Admin':
      return 'Admin';
    case 'OrganizationSuperAdmin':
      return 'Org Super Admin';
    case 'OrganizationAdmin':
      return 'Org Admin';
    case 'User':
      return 'Korisnik';
    default:
      return role;
  }
}

Color roleBadgeColor(String role, bool isDark) {
  switch (role) {
    case 'SuperAdmin':
      return AppColors.rolePurple;
    case 'Admin':
      return AppColors.info;
    case 'OrganizationSuperAdmin':
      return isDark ? AppColors.secondary : AppColors.primary;
    case 'OrganizationAdmin':
      return AppColors.roleCyan;
    case 'User':
      return isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    default:
      return isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
  }
}
