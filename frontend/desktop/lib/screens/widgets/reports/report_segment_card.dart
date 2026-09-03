import 'package:flutter/material.dart';

import '../../../core/formatting.dart';
import '../../../models/responses/report_responses.dart';
import '../../../theme/app_colors.dart';

/// One audience segment.
///
/// Two bars rather than one: a segment's share of *buyers* and its share of
/// *revenue* are the whole point of segmenting, and the gap between them is the
/// finding. Drawn as a pair so that gap is visible without reading either
/// number.
///
/// Below them, four stats fill in the rest of the RFM picture the bars can't
/// show on their own: how many buyers are in the segment, what they spend, how
/// often they buy, and how recently they last did. Every one is a real field on
/// `AudienceSegment` — nothing here is invented to fill a grid, it is what the
/// server already computed and previously left unused.
///
/// The card re-flows on its own width rather than assuming the narrow column it
/// was first drawn in. Given room it puts the two bars beside each other and the
/// four stats in one line; the card gets denser and shorter instead of flinging
/// a label to one edge and its figure to the other, which is what a 900px-wide
/// copy of the narrow layout looked like on a maximised window.
class ReportSegmentCard extends StatelessWidget {
  final AudienceSegment segment;

  /// The segments are ranked by value server-side, so the first card is the
  /// most valuable group and is drawn in the brand accent.
  final bool isLeading;

  const ReportSegmentCard({super.key, required this.segment, this.isLeading = false});

  /// Where the card switches to its wide arrangement. Four stats abreast need
  /// about 120px each for "Prosječna potrošnja" to survive without an ellipsis,
  /// and the two share bars need roughly 200px each to be worth reading.
  static const double _wideBreakpoint = 480;

  /// Below this a card's two labelled bars and four stats stop being readable,
  /// so a row of them drops to fewer columns instead.
  static const double minWidth = 300;

  /// How many of these go across a row [width] wide.
  ///
  /// The count returned is always a *divisor* of [count], and that is the whole
  /// rule: three segments go three-across or one-across, never two-and-a-hole,
  /// and four go four, two or one. K-Means asks for four clusters and drops the
  /// empty ones, so an odd count is routine — laying three out two-across is
  /// what left half a row of white space beside the last card.
  ///
  /// The widest arrangement whose cards still clear [minWidth] wins; the card
  /// re-flows its own insides above [_wideBreakpoint], so a wide card answers
  /// with more density rather than more stretch.
  static int columnsFor(int count, double width, {double gap = 16}) {
    for (var columns = count; columns > 1; columns--) {
      if (count % columns != 0) continue;
      if ((width - gap * (columns - 1)) / columns >= minWidth) return columns;
    }
    return 1;
  }

  @override
  Widget build(BuildContext context) {
    final brightness = Theme.of(context).brightness;
    final isDark = brightness == Brightness.dark;
    final accent = isLeading ? AppColors.secondary : AppColors.textTertiary(brightness);

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurface : Colors.white,
        border: Border.all(color: AppColors.border(brightness)),
        borderRadius: BorderRadius.circular(14),
      ),
      child: LayoutBuilder(
        builder: (context, constraints) {
          final isWide = constraints.maxWidth >= _wideBreakpoint;

          return Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      segment.name,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        fontSize: 13.5,
                        fontWeight: FontWeight.w700,
                        color: AppColors.textPrimary(brightness),
                      ),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Text(
                    '${formatCount(segment.buyers)} kupaca',
                    style: TextStyle(fontSize: 12, color: AppColors.textTertiary(brightness)),
                  ),
                ],
              ),
              const SizedBox(height: 4),
              Text(
                segment.description,
                style:
                    TextStyle(fontSize: 12, height: 1.4, color: AppColors.textSecondary(brightness)),
              ),
              const SizedBox(height: 14),
              _shareBars(brightness, accent, isWide),
              const SizedBox(height: 12),
              _stats(brightness, isWide),
            ],
          );
        },
      ),
    );
  }

  /// Buyer share above revenue share when narrow, side by side when wide. The
  /// pair is the finding either way — what matters is that both are on screen
  /// together, not which axis separates them.
  Widget _shareBars(Brightness brightness, Color accent, bool isWide) {
    final buyers = _ShareBar(
      label: 'Udio kupaca',
      percent: segment.sharePercent,
      color: accent.withValues(alpha: 0.45),
      brightness: brightness,
    );
    final revenue = _ShareBar(
      label: 'Udio prihoda',
      percent: segment.revenueSharePercent,
      color: accent,
      brightness: brightness,
    );

    if (!isWide) {
      return Column(children: [buyers, const SizedBox(height: 8), revenue]);
    }

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(child: buyers),
        const SizedBox(width: 16),
        Expanded(child: revenue),
      ],
    );
  }

  /// One row of four when there is room, a 2×2 grid when there isn't — the card
  /// always states all four figures, only the shape of the grid changes.
  Widget _stats(Brightness brightness, bool isWide) {
    final stats = [
      _Stat(
        label: 'Prosječna potrošnja',
        value: formatMoney(segment.averageSpend),
        brightness: brightness,
      ),
      _Stat(
        label: 'Zadnja kupovina',
        value: '${formatCount(segment.averageRecencyDays)} d',
        brightness: brightness,
      ),
      _Stat(
        label: 'Broj kupaca',
        value: formatCount(segment.buyers),
        brightness: brightness,
      ),
      _Stat(
        label: 'Prosječno karata',
        value: _formatOneDecimal(segment.averageTickets),
        brightness: brightness,
      ),
    ];

    if (isWide) {
      return Row(children: [for (final stat in stats) Expanded(child: stat)]);
    }

    return Column(
      children: [
        Row(children: [Expanded(child: stats[0]), Expanded(child: stats[1])]),
        const SizedBox(height: 10),
        Row(children: [Expanded(child: stats[2]), Expanded(child: stats[3])]),
      ],
    );
  }
}

class _ShareBar extends StatelessWidget {
  final String label;
  final double percent;
  final Color color;
  final Brightness brightness;

  const _ShareBar({
    required this.label,
    required this.percent,
    required this.color,
    required this.brightness,
  });

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                label,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontSize: 11, color: AppColors.textTertiary(brightness)),
              ),
            ),
            Text(
              formatPercent(percent),
              style: TextStyle(
                fontSize: 11,
                fontWeight: FontWeight.w700,
                color: AppColors.textSecondary(brightness),
              ),
            ),
          ],
        ),
        const SizedBox(height: 4),
        ClipRRect(
          borderRadius: BorderRadius.circular(4),
          child: LinearProgressIndicator(
            // Clamped: a share is a percentage of a whole and cannot exceed it,
            // but a rounding artefact must not throw off the track's layout.
            value: (percent / 100).clamp(0.0, 1.0),
            minHeight: 6,
            backgroundColor: brightness == Brightness.dark
                ? AppColors.darkSurfaceMuted
                : AppColors.lightSurfaceMuted,
            valueColor: AlwaysStoppedAnimation<Color>(color),
          ),
        ),
      ],
    );
  }
}

/// `4,5` — one decimal, comma separator, no unit. The same rounding
/// `formatPercent` uses, minus the `%`: there is no shared formatter for a
/// bare decimal in `core/formatting.dart`, and adding a one-off percent-less
/// variant there for a single caller wasn't worth widening that file's surface.
String _formatOneDecimal(double value) {
  final tenths = (value.abs() * 10).round();
  final sign = value < 0 ? '-' : '';
  return '$sign${formatCount(tenths ~/ 10)},${tenths % 10}';
}

class _Stat extends StatelessWidget {
  final String label;
  final String value;
  final Brightness brightness;

  const _Stat({required this.label, required this.value, required this.brightness});

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: TextStyle(fontSize: 11, color: AppColors.textTertiary(brightness))),
        const SizedBox(height: 2),
        Text(
          value,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w700,
            color: AppColors.textPrimary(brightness),
          ),
        ),
      ],
    );
  }
}
