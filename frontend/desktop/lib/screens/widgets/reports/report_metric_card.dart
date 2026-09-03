import 'package:flutter/material.dart';

import '../../../theme/app_colors.dart';

/// How a figure reads. Kept as an enum rather than a raw `Color` so the light
/// and dark palettes are resolved in one place — the design is light-only, and
/// `#15803D` on the app's near-black surface is too dark to read.
enum ReportEmphasis { neutral, positive, negative }

/// One headline figure: a label, a big value, and an optional hint line.
///
/// Not `StatCard` (the shared label/value tile): these carry a third line and
/// an optional leading icon roundel, which the design's redemption tiles need
/// and which would bloat `StatCard` for its five existing callers.
class ReportMetricCard extends StatelessWidget {
  final String label;
  final String value;
  final String? hint;
  final ReportEmphasis emphasis;
  final IconData? icon;

  const ReportMetricCard({
    super.key,
    required this.label,
    required this.value,
    this.hint,
    this.emphasis = ReportEmphasis.neutral,
    this.icon,
  });

  /// Resolves the accent for the current theme. The design's `#15803D`
  /// (`successDark`) is reserved for light mode; dark mode steps up to the
  /// brighter `success` so the number keeps its contrast against `darkSurface`.
  static Color colorFor(ReportEmphasis emphasis, Brightness brightness) {
    final isDark = brightness == Brightness.dark;
    return switch (emphasis) {
      ReportEmphasis.positive => isDark ? AppColors.success : AppColors.successDark,
      ReportEmphasis.negative => isDark ? AppColors.error : AppColors.errorDark,
      ReportEmphasis.neutral => AppColors.textPrimary(brightness),
    };
  }

  @override
  Widget build(BuildContext context) {
    final brightness = Theme.of(context).brightness;
    final isDark = brightness == Brightness.dark;
    final accent = colorFor(emphasis, brightness);

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurface : Colors.white,
        border: Border.all(color: AppColors.border(brightness)),
        borderRadius: BorderRadius.circular(16),
        boxShadow: isDark
            ? null
            : [
                BoxShadow(
                  color: Colors.black.withValues(alpha: 0.03),
                  blurRadius: 8,
                  offset: const Offset(0, 2),
                ),
              ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          if (icon != null) ...[
            Container(
              width: 32,
              height: 32,
              decoration: BoxDecoration(
                color: accent.withValues(alpha: isDark ? 0.18 : 0.1),
                borderRadius: BorderRadius.circular(9),
              ),
              child: Icon(icon, size: 16, color: accent),
            ),
            const SizedBox(height: 10),
          ],
          Text(
            label,
            style: TextStyle(fontSize: 13, color: AppColors.textTertiary(brightness)),
          ),
          const SizedBox(height: 4),
          // FittedBox, not a smaller font: a platform-wide yearly revenue is a
          // long string, and shrinking it to fit beats clipping it or wrapping
          // a number across two lines.
          FittedBox(
            fit: BoxFit.scaleDown,
            alignment: Alignment.centerLeft,
            child: Text(
              value,
              maxLines: 1,
              style: TextStyle(fontSize: 24, fontWeight: FontWeight.w700, color: accent),
            ),
          ),
          if (hint != null) ...[
            const SizedBox(height: 5),
            Text(
              hint!,
              style: TextStyle(fontSize: 11, color: AppColors.textTertiary(brightness)),
            ),
          ],
        ],
      ),
    );
  }
}

/// The white bordered panel every block on the reports screen sits in — the
/// design's `background:#fff; border:1px solid #E5E7EB; border-radius:16px`
/// card, with an optional heading.
class ReportCard extends StatelessWidget {
  final String? title;
  final String? subtitle;
  final Widget child;
  final EdgeInsetsGeometry padding;

  const ReportCard({
    super.key,
    this.title,
    this.subtitle,
    required this.child,
    this.padding = const EdgeInsets.all(20),
  });

  @override
  Widget build(BuildContext context) {
    final brightness = Theme.of(context).brightness;
    final isDark = brightness == Brightness.dark;

    return Container(
      padding: padding,
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurface : Colors.white,
        border: Border.all(color: AppColors.border(brightness)),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          if (title != null)
            Text(
              title!,
              style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
            ),
          if (subtitle != null) ...[
            const SizedBox(height: 4),
            Text(
              subtitle!,
              style: TextStyle(fontSize: 12, color: AppColors.textTertiary(brightness)),
            ),
          ],
          if (title != null || subtitle != null) const SizedBox(height: 16),
          child,
        ],
      ),
    );
  }
}
