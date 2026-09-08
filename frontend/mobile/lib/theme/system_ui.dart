import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'app_colors.dart';

/// Android system-bar treatment for the whole app.
///
/// Two separate problems, one place to fix them:
///
/// 1. **Icon contrast.** The status/navigation bar icons are drawn by the OS, not by us, so they
///    have to be told which way to go. Dark icons over our light surfaces, light icons over the
///    dark theme. Getting this wrong makes the bars look empty rather than wrong, which is why it
///    goes unnoticed.
/// 2. **The app drawing behind the navigation bar.** From Android 15 (API 35) every app is
///    edge-to-edge and `systemNavigationBarColor` is ignored outright — the OS will not give an
///    app an opaque bar any more, so the app's own pixels end up under a transparent strip that
///    still swallows every touch. Text scrolling under the gesture pill and buttons that look
///    tappable but aren't both come from that. [SystemBarInset] ends it by taking the strip out of
///    the app's usable area altogether.
class SystemUi {
  SystemUi._();

  static SystemUiOverlayStyle styleFor(Brightness brightness) {
    final isDark = brightness == Brightness.dark;
    // Brightness.light here means "light icons" — the value names the icons, not the background,
    // which is the single easiest thing to invert in this API.
    final iconBrightness = isDark ? Brightness.light : Brightness.dark;

    return SystemUiOverlayStyle(
      statusBarColor: Colors.transparent,
      statusBarIconBrightness: iconBrightness,
      statusBarBrightness: brightness,
      // Honoured below API 35 and ignored at or above it. Kept so older devices match what
      // SystemBarInset paints, rather than showing two different colours on two Android versions.
      systemNavigationBarColor: isDark ? AppColors.darkSurface : AppColors.lightSurface,
      systemNavigationBarIconBrightness: iconBrightness,
    );
  }
}

/// Ends the app's layout above the Android navigation bar, and paints the strip it leaves behind
/// in a solid colour.
///
/// Wrapped once around the whole app in `main.dart`, never per screen. The [ColoredBox] covers the
/// full window — including the navigation bar strip — while the [SafeArea] inside it pushes every
/// screen up clear of that strip. So the bar reads as one solid colour, and nothing the app draws
/// can land underneath it.
///
/// Doing this globally rather than bar-by-bar is deliberate. The previous attempt padded each
/// bottom bar individually, which left every screen *without* one (a scrolling list, a QR code, a
/// form) still running its content under the navigation bar. There is no screen where drawing
/// under it is wanted, so the inset belongs above all of them.
///
/// `SafeArea` consumes the bottom padding from the `MediaQuery` it passes down, so any `SafeArea`
/// further down the tree becomes a no-op instead of insetting a second time. The keyboard is
/// unaffected: its inset arrives as `viewInsets`, which this does not touch.
class SystemBarInset extends StatelessWidget {
  final Widget child;

  const SystemBarInset({super.key, required this.child});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return ColoredBox(
      color: isDark ? AppColors.darkSurface : AppColors.lightSurface,
      // Bottom only. The status bar stays edge-to-edge — screens with a hero image deliberately
      // run it up under the clock, and they handle their own top inset.
      child: SafeArea(
        top: false,
        left: false,
        right: false,
        child: child,
      ),
    );
  }
}
