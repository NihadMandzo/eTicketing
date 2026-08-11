import 'package:flutter/material.dart';

/// Canonical color tokens shared across all eTicketing frontends (web,
/// desktop, mobile). Values are duplicated in `frontend/desktop/lib/theme` —
/// each app is a separate Flutter project, so this file is the mobile
/// app's own copy rather than an imported shared package.
///
/// Keep these in sync with `frontend/web/src/styles.css`'s `:root`/`.dark`
/// blocks when the palette changes.
class AppColors {
  AppColors._();

  // Brand
  static const primary = Color(0xFF0D7C66);
  static const primaryDark = Color(0xFF0A6B57);
  static const secondary = Color(0xFF41C9B4);
  static const accent = Color(0xFFFF6F3C);

  // Light surfaces / text
  static const lightBackground = Color(0xFFFAFAFA);
  static const lightSurface = Color(0xFFFFFFFF);
  static const lightSurfaceSubtle = Color(0xFFF9FAFB);
  static const lightSurfaceMuted = Color(0xFFF3F4F6);
  static const lightSurfaceTint = Color(0xFFF0FDF9);
  static const lightInputFill = Color(0xFFFAFAFA);
  static const lightBorder = Color(0xFFE5E7EB);
  static const lightBorderInput = Color(0xFFD1D5DB);
  static const lightTextPrimary = Color(0xFF111827);
  static const lightTextSecondary = Color(0xFF374151);
  static const lightTextTertiary = Color(0xFF6B7280);
  static const lightTextDisabled = Color(0xFF9CA3AF);

  // Dark surfaces / text — kept in the same teal-tinted hue family as
  // darkBackground/darkSurface (previously darkSurfaceMuted/darkBorder/
  // darkBorderInput/darkTextTertiary/darkTextDisabled were plain blue-slate
  // grays, which clashed against the teal-black background/card).
  static const darkBackground = Color(0xFF0A0F0D);
  static const darkSurface = Color(0xFF111816);
  static const darkSurfaceMuted = Color(0xFF17221E);
  static const darkSurfaceTint = Color(0xFF14201C);
  static const darkInputFill = Color(0xFF17221E);
  static const darkBorder = Color(0xFF2A3B36);
  static const darkBorderInput = Color(0xFF3D5049);
  static const darkTextPrimary = Color(0xFFF9FAFB);
  static const darkTextSecondary = Color(0xFFE5E7EB);
  static const darkTextTertiary = Color(0xFF93A29D);
  static const darkTextDisabled = Color(0xFF6B7A75);

  // Status
  static const success = Color(0xFF16A34A);
  static const error = Color(0xFFEF4444);
  // Use for solid red button/snackbar fills with white text — plain `error`
  // only gives ~3.8:1 contrast against white, which fails WCAG AA (4.5:1).
  static const errorDark = Color(0xFFDC2626);
  static const errorBg = Color(0xFFFEF2F2);
  // Darkened from #7F1D1D so `error` (#EF4444) text/icons on top of it reach
  // ≥4.5:1 contrast (was ~2.7:1) — see desktop/theme/app_colors.dart.
  static const errorBgDarkMode = Color(0xFF2A1214);
}
