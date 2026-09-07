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
/// 2. **The app drawing behind the navigation bar.** From Android 15 (API 35) the system bars are
///    force-transparent and `systemNavigationBarColor` is ignored outright — an app cannot ask the
///    OS for an opaque bar any more. The only thing that still works on every version is to paint
///    that strip ourselves, which is what [SystemBarBackdrop] is for. The colour set here is the
///    fallback that older Androids (and the brief moment before first frame) still honour, so the
///    two paths agree instead of flashing a different colour at each other.
class SystemUi {
  SystemUi._();

  static SystemUiOverlayStyle styleFor(Brightness brightness) {
    final isDark = brightness == Brightness.dark;
    final surface = isDark ? AppColors.darkSurface : AppColors.lightSurface;
    // Brightness.light here means "light icons" — the value names the icons, not the background,
    // which is the single easiest thing to invert in this API.
    final iconBrightness = isDark ? Brightness.light : Brightness.dark;

    return SystemUiOverlayStyle(
      statusBarColor: Colors.transparent,
      statusBarIconBrightness: iconBrightness,
      statusBarBrightness: brightness,
      systemNavigationBarColor: surface,
      systemNavigationBarDividerColor: isDark ? AppColors.darkBorder : AppColors.lightBorder,
      systemNavigationBarIconBrightness: iconBrightness,
    );
  }
}

/// Paints an opaque strip of [color] behind the system navigation bar and puts the same inset
/// back as padding, so [child] never lands underneath it.
///
/// Used instead of a plain `SafeArea` by every bar that sits at the bottom of the screen: a
/// `SafeArea` pads *around* its child, which leaves the strip behind the navigation bar showing
/// whatever is beneath — the scaffold, or worse, a list scrolling past. This paints first and pads
/// second, so the bar reads as one solid surface that ends where the phone's own bar begins.
class SystemBarBackdrop extends StatelessWidget {
  final Widget child;

  /// Defaults to the theme's surface — pass a colour only when the bar it backs is not on surface.
  final Color? color;

  const SystemBarBackdrop({super.key, required this.child, this.color});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    // viewPadding, not padding: `padding` is already zeroed out by an enclosing SafeArea/MediaQuery
    // consumer, and reading it would silently stop reserving the inset the moment this widget is
    // nested inside one.
    final inset = MediaQuery.viewPaddingOf(context).bottom;

    return Container(
      color: color ?? (isDark ? AppColors.darkSurface : AppColors.lightSurface),
      padding: EdgeInsets.only(bottom: inset),
      child: child,
    );
  }
}
