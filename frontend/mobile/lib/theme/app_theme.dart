import 'package:flutter/material.dart';

import 'app_colors.dart';

/// Light/dark [ThemeData] built from [AppColors]. Wired into `MaterialApp`
/// in `main.dart` via `theme`/`darkTheme`/`themeMode`.
class AppTheme {
  AppTheme._();

  static ThemeData get light => _build(Brightness.light);
  static ThemeData get dark => _build(Brightness.dark);

  static ThemeData _build(Brightness brightness) {
    final isDark = brightness == Brightness.dark;

    final colorScheme = ColorScheme.fromSeed(
      seedColor: AppColors.primary,
      brightness: brightness,
      primary: isDark ? AppColors.secondary : AppColors.primary,
      onPrimary: isDark ? AppColors.darkBackground : Colors.white,
      secondary: isDark ? AppColors.primary : AppColors.secondary,
      error: AppColors.error,
      surface: isDark ? AppColors.darkSurface : AppColors.lightSurface,
      onSurface: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
    );

    final borderColor = isDark ? AppColors.darkBorder : AppColors.lightBorder;
    final inputBorderColor = isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput;
    final inputFill = isDark ? AppColors.darkInputFill : AppColors.lightInputFill;
    final primary = isDark ? AppColors.secondary : AppColors.primary;

    return ThemeData(
      useMaterial3: true,
      brightness: brightness,
      colorScheme: colorScheme,
      scaffoldBackgroundColor: isDark ? AppColors.darkBackground : AppColors.lightBackground,
      cardColor: isDark ? AppColors.darkSurface : AppColors.lightSurface,
      dividerColor: borderColor,
      cardTheme: CardThemeData(
        color: isDark ? AppColors.darkSurface : AppColors.lightSurface,
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(14),
          side: BorderSide(color: borderColor),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: inputFill,
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: BorderSide(color: inputBorderColor),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: BorderSide(color: inputBorderColor),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: BorderSide(color: primary, width: 2),
        ),
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: primary,
          foregroundColor: isDark ? AppColors.darkBackground : Colors.white,
          elevation: 0,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        ),
      ),
      // Material 3 shapes FilledButton/OutlinedButton/TextButton as stadiums by default, which is
      // where the pill-shaped "Plati" button came from. Every other surface in this app — cards,
      // inputs, the filter dropdowns — is a 12-14px rounded rectangle, so a fully-round button was
      // the one shape that belonged to nothing else on screen. Set once here rather than
      // per-button, so the three clients' primary actions cannot drift apart again.
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
          textStyle: const TextStyle(fontSize: 14, fontWeight: FontWeight.w700),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
          textStyle: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
        ),
      ),
      textTheme: (isDark ? ThemeData.dark() : ThemeData.light()).textTheme.apply(
            bodyColor: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
            displayColor: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
          ),
      appBarTheme: AppBarTheme(
        backgroundColor: isDark ? AppColors.darkSurface : AppColors.lightSurface,
        foregroundColor: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary,
        elevation: 0,
        // Matches the design mockup's sub-screen headers (Detalji događaja,
        // Ulaznica, Plaćanje) — a back chevron with a centered title, not
        // Android's default left-aligned AppBar title.
        centerTitle: true,
      ),
    );
  }
}
