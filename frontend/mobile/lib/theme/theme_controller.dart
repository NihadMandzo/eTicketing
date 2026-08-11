import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// App-wide light/dark preference, persisted locally to this device only
/// (this app's own `shared_preferences` store — independent of the web and
/// desktop apps' preferences). Mirrors the `ValueNotifier`-based state
/// pattern already used by `Session` in `core/session.dart` rather than
/// pulling in a state-management package.
class ThemeController {
  ThemeController._();

  static const _prefsKey = 'theme_mode';

  static final ValueNotifier<ThemeMode> mode = ValueNotifier(ThemeMode.system);

  /// Loads the persisted preference. Call once during app startup, before
  /// `runApp`, alongside `initApiClient()`.
  static Future<void> init() async {
    final prefs = await SharedPreferences.getInstance();
    final stored = prefs.getString(_prefsKey);
    mode.value = switch (stored) {
      'light' => ThemeMode.light,
      'dark' => ThemeMode.dark,
      _ => ThemeMode.system,
    };
  }

  static Future<void> setMode(ThemeMode newMode) async {
    mode.value = newMode;
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_prefsKey, newMode.name);
  }

  /// Flips between light and dark (system is treated as light for the
  /// purposes of the toggle — resolved via [isDark]).
  static Future<void> toggle(BuildContext context) async {
    final resolvedDark = isDark(context);
    await setMode(resolvedDark ? ThemeMode.light : ThemeMode.dark);
  }

  static bool isDark(BuildContext context) {
    return switch (mode.value) {
      ThemeMode.dark => true,
      ThemeMode.light => false,
      ThemeMode.system => MediaQuery.platformBrightnessOf(context) == Brightness.dark,
    };
  }
}
