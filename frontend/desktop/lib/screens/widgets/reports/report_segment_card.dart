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
/// Below them, a 2×2 grid of four stats fills in the rest of the RFM picture
/// the bars can't show on their own: how many buyers are in the segment, what
/// they spend, how often they buy, and how recently they last did. Every one
/// is a real field on `AudienceSegment` — nothing here is invented to fill a
/// grid, it is what the server already computed and previously left unused.
class ReportSegmentCard extends StatelessWidget {
  final AudienceSegment segment;

  /// The segments are ranked by value server-side, so the first card is the
  /// most valuable group and is drawn in the brand accent.
  final bool isLeading;

  const ReportSegmentCard({super.key, required this.segment, this.isLeading = false});

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
      child: Column(
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
            style: TextStyle(fontSize: 12, height: 1.4, color: AppColors.textSecondary(brightness)),
          ),
          const SizedBox(height: 14),
          _ShareBar(
            label: 'Udio kupaca',
            percent: segment.sharePercent,
            color: accent.withValues(alpha: 0.45),
            brightness: brightness,
          ),
          const SizedBox(height: 8),
          _ShareBar(
            label: 'Udio prihoda',
            percent: segment.revenueSharePercent,
            color: accent,
            brightness: brightness,
          ),
          const SizedBox(height: 12),
          // Two rows of two — a 2×2 grid rather than a single row, so the card
          // states all four figures at once instead of only the two that used
          // to fit beside each other.
          Row(
            children: [
              Expanded(
                child: _Stat(
                  label: 'Prosječna potrošnja',
                  value: formatMoney(segment.averageSpend),
                  brightness: brightness,
                ),
              ),
              Expanded(
                child: _Stat(
                  label: 'Zadnja kupovina',
                  value: '${formatCount(segment.averageRecencyDays)} d',
                  brightness: brightness,
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: _Stat(
                  label: 'Broj kupaca',
                  value: formatCount(segment.buyers),
                  brightness: brightness,
                ),
              ),
              Expanded(
                child: _Stat(
                  label: 'Prosječno karata',
                  value: _formatOneDecimal(segment.averageTickets),
                  brightness: brightness,
                ),
              ),
            ],
          ),
        ],
      ),
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
