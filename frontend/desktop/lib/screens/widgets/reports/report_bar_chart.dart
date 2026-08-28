import 'package:flutter/material.dart';

import '../../../theme/app_colors.dart';

/// One bar: the label under it, the figure above it, and how tall it is
/// relative to the tallest bar in the series.
class ReportBarData {
  final String label;
  final String value;

  /// 0-1, relative to the series peak. The caller scales, because only it
  /// knows whether the series is money or a count.
  final double ratio;

  const ReportBarData({required this.label, required this.value, required this.ratio});
}

/// The sales / arrivals column chart.
///
/// Hand-built out of `Container`s rather than pulled from a charting package:
/// the design is a plain gradient column with a value above and a label below,
/// and `pubspec.yaml` deliberately keeps its dependency list short
/// (.claude/rules/21-frontend-desktop.md — don't add a package without
/// checking first).
class ReportBarChart extends StatelessWidget {
  final List<ReportBarData> bars;
  final double height;

  /// Highlights the tallest bar with the solid brand gradient and mutes the
  /// rest — the treatment the design's "Dolazak po satu" chart uses to make the
  /// peak hour findable at a glance. Off for the revenue chart, where every bar
  /// matters equally.
  final bool highlightPeak;

  const ReportBarChart({
    super.key,
    required this.bars,
    this.height = 180,
    this.highlightPeak = false,
  });

  @override
  Widget build(BuildContext context) {
    final brightness = Theme.of(context).brightness;

    if (bars.isEmpty) {
      return SizedBox(
        height: height,
        child: Center(
          child: Text(
            'Nema podataka za odabrani period.',
            style: TextStyle(fontSize: 13, color: AppColors.textTertiary(brightness)),
          ),
        ),
      );
    }

    final peak = bars.map((b) => b.ratio).reduce((a, b) => a > b ? a : b);

    // Horizontally scrollable below a minimum per-bar width: a 12-month or
    // 24-hour series squeezed into a narrow window turns every label into an
    // ellipsis, so the chart scrolls instead of shrinking past legibility.
    return LayoutBuilder(
      builder: (context, constraints) {
        const minBarWidth = 44.0;
        final needed = bars.length * minBarWidth;
        final chart = SizedBox(
          height: height,
          width: needed > constraints.maxWidth ? needed : constraints.maxWidth,
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              for (final bar in bars)
                Expanded(
                  child: _Bar(
                    data: bar,
                    isPeak: highlightPeak && peak > 0 && bar.ratio >= peak,
                    muted: highlightPeak,
                  ),
                ),
            ],
          ),
        );

        return needed > constraints.maxWidth
            ? SingleChildScrollView(scrollDirection: Axis.horizontal, child: chart)
            : chart;
      },
    );
  }
}

class _Bar extends StatelessWidget {
  final ReportBarData data;
  final bool isPeak;
  final bool muted;

  const _Bar({required this.data, required this.isPeak, required this.muted});

  @override
  Widget build(BuildContext context) {
    final brightness = Theme.of(context).brightness;
    final ratio = data.ratio.clamp(0.0, 1.0);
    final solid = !muted || isPeak;

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 4),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.end,
        children: [
          Text(
            data.value,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              fontSize: 10,
              fontWeight: FontWeight.w700,
              color: AppColors.textTertiary(brightness),
            ),
          ),
          const SizedBox(height: 6),
          // The bar occupies its share of the remaining vertical space; the
          // Spacer above it is what makes every bar in the row share a baseline.
          Expanded(
            child: Column(
              children: [
                Expanded(flex: ((1 - ratio) * 1000).round(), child: const SizedBox()),
                Expanded(
                  flex: (ratio * 1000).round().clamp(1, 1000),
                  child: Container(
                    constraints: const BoxConstraints(maxWidth: 34, minHeight: 4),
                    decoration: BoxDecoration(
                      gradient: solid
                          ? const LinearGradient(
                              begin: Alignment.topCenter,
                              end: Alignment.bottomCenter,
                              colors: [AppColors.secondary, AppColors.primary],
                            )
                          : null,
                      color: solid ? null : AppColors.primary.withValues(alpha: 0.28),
                      borderRadius: const BorderRadius.vertical(top: Radius.circular(6)),
                    ),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 8),
          Text(
            data.label,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(fontSize: 11, color: AppColors.textTertiary(brightness)),
          ),
        ],
      ),
    );
  }
}
