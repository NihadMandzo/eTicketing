import 'package:flutter/material.dart';

/// Canonical color tokens shared across all eTicketing frontends (web,
/// desktop, mobile). Values are duplicated in `frontend/mobile/lib/theme` —
/// each app is a separate Flutter project, so this file is the desktop
/// app's own copy rather than an imported shared package.
///
/// Keep these in sync with `frontend/web/src/styles.css`'s `:root`/`.dark`
/// blocks when the palette changes.
class AppColors {
  AppColors._();

  // Brand — eKarta green palette (#1D5B3A dark / #6FAE4A mid / #8DC63F
  // bright), replacing the earlier teal/coral scheme.
  static const primary = Color(0xFF1D5B3A);
  static const primaryDark = Color(0xFF164A2F);
  static const primaryDarkest = Color(0xFF0F3521);
  static const secondary = Color(0xFF6FAE4A);
  static const accent = Color(0xFF8DC63F);

  // Light surfaces / text
  static const lightBackground = Color(0xFFFAFAFA);
  static const lightSurface = Color(0xFFFFFFFF);
  static const lightSurfaceSubtle = Color(0xFFF9FAFB);
  static const lightSurfaceMuted = Color(0xFFF3F4F6);
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
  static const darkSurfaceSubtle = Color(0xFF16201D);
  static const darkSurfaceMuted = Color(0xFF17221E);
  static const darkInputFill = Color(0xFF17221E);
  static const darkBorder = Color(0xFF2A3B36);
  static const darkBorderInput = Color(0xFF3D5049);
  static const darkTextPrimary = Color(0xFFF9FAFB);
  static const darkTextSecondary = Color(0xFFE5E7EB);
  static const darkTextTertiary = Color(0xFF93A29D);
  static const darkTextDisabled = Color(0xFF6B7A75);

  // Status — canonical (folds the old snackbar-only #2E7D32/#C62828 pair
  // into the same values used by status badges everywhere else)
  static const success = Color(0xFF16A34A);
  static const successDark = Color(0xFF15803D);
  static const successBg = Color(0xFFDCFCE7);
  static const successBgDark = Color(0xFF14532D);

  static const error = Color(0xFFEF4444);
  static const errorDark = Color(0xFFDC2626);
  static const errorDarkest = Color(0xFF991B1B);
  static const errorText = Color(0xFFB91C1C);
  static const errorBg = Color(0xFFFEF2F2);
  static const errorBorder = Color(0xFFFECACA);
  // Darkened from #7F1D1D — that was too bright/saturated to give AppColors
  // .error (#EF4444) text/icons the ≥4.5:1 contrast WCAG AA needs (it only
  // hit ~2.7:1). This dark, desaturated red gives ~4.7:1 while still reading
  // clearly as a danger surface against the app's near-black dark theme.
  static const errorBgDarkMode = Color(0xFF2A1214);

  static const warning = Color(0xFFF59E0B);
  static const warningDark = Color(0xFFD97706);
  static const warningBg = Color(0xFFFFFBEB);
  static const warningBorder = Color(0xFFFDE68A);

  static const info = Color(0xFF2563EB);
  static const infoBg = Color(0xFFEFF6FF);
  static const infoBorder = Color(0xFFBFDBFE);

  // Role badges (admin-only)
  static const rolePurple = Color(0xFF7C3AED);
  static const rolePurpleBg = Color(0xFFEDE9FE);
  static const roleCyan = Color(0xFF0891B2);
  static const roleCyanBg = Color(0xFFCFFAFE);

  /// Resolves the appropriate palette for the given brightness — prefer
  /// `Theme.of(context).colorScheme`/`AppTheme` for widget styling; this is
  /// a convenience for the few spots that need a raw color pair.
  static Color surface(Brightness b) => b == Brightness.dark ? darkSurface : lightSurface;
  static Color background(Brightness b) => b == Brightness.dark ? darkBackground : lightBackground;
  static Color border(Brightness b) => b == Brightness.dark ? darkBorder : lightBorder;
  static Color textPrimary(Brightness b) => b == Brightness.dark ? darkTextPrimary : lightTextPrimary;
  static Color textSecondary(Brightness b) => b == Brightness.dark ? darkTextSecondary : lightTextSecondary;
  static Color textTertiary(Brightness b) => b == Brightness.dark ? darkTextTertiary : lightTextTertiary;
}
