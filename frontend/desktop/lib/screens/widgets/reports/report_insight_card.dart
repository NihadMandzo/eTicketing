import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../../models/responses/report_responses.dart';
import '../../../theme/app_colors.dart';

/// One generated finding on the AI Uvidi tab.
///
/// A left accent bar rather than a tinted panel: these stack four to six deep,
/// and four filled colour blocks in a column turn the page into a traffic
/// light. The severity has to be readable at a glance without shouting.
class ReportInsightCard extends StatelessWidget {
  final BusinessInsight insight;

  const ReportInsightCard({super.key, required this.insight});

  @override
  Widget build(BuildContext context) {
    final brightness = Theme.of(context).brightness;
    final isDark = brightness == Brightness.dark;
    final accent = _accent(brightness);

    return Container(
      padding: const EdgeInsets.fromLTRB(16, 14, 16, 14),
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurface : Colors.white,
        border: Border(
          left: BorderSide(color: accent, width: 3),
          top: BorderSide(color: AppColors.border(brightness)),
          right: BorderSide(color: AppColors.border(brightness)),
          bottom: BorderSide(color: AppColors.border(brightness)),
        ),
        borderRadius: const BorderRadius.horizontal(
          left: Radius.circular(4),
          right: Radius.circular(14),
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 32,
            height: 32,
            decoration: BoxDecoration(
              color: accent.withValues(alpha: isDark ? 0.18 : 0.10),
              borderRadius: BorderRadius.circular(9),
            ),
            child: Icon(_icon, size: 16, color: accent),
          ),
          const SizedBox(width: 12),
          // Expanded, not bare: the body is a full Bosnian sentence and would
          // overflow the Row at any narrow window width
          // (.claude/rules/21-frontend-desktop.md).
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  insight.title,
                  style: TextStyle(
                    fontSize: 13.5,
                    fontWeight: FontWeight.w700,
                    color: AppColors.textPrimary(brightness),
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  insight.body,
                  style: TextStyle(
                    fontSize: 12.5,
                    height: 1.45,
                    color: AppColors.textSecondary(brightness),
                  ),
                ),
              ],
            ),
          ),
          if (insight.metric != null) ...[
            const SizedBox(width: 12),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
              decoration: BoxDecoration(
                color: accent.withValues(alpha: isDark ? 0.18 : 0.10),
                borderRadius: BorderRadius.circular(20),
              ),
              child: Text(
                insight.metric!,
                style: TextStyle(fontSize: 12, fontWeight: FontWeight.w700, color: accent),
              ),
            ),
          ],
        ],
      ),
    );
  }

  /// Same light/dark split as `ReportMetricCard.colorFor`: the design's darker
  /// greens and reds are unreadable on the app's near-black surface.
  Color _accent(Brightness brightness) {
    final isDark = brightness == Brightness.dark;
    return switch (insight.severity) {
      InsightSeverity.critical => isDark ? AppColors.error : AppColors.errorDark,
      InsightSeverity.warning => isDark ? AppColors.warning : AppColors.warningDark,
      InsightSeverity.positive => isDark ? AppColors.success : AppColors.successDark,
      InsightSeverity.neutral => AppColors.textTertiary(brightness),
    };
  }

  IconData get _icon => switch (insight.category) {
        InsightCategory.forecast => LucideIcons.trendingUp,
        InsightCategory.anomaly => LucideIcons.triangleAlert,
        InsightCategory.sales => LucideIcons.banknote,
        InsightCategory.audience => LucideIcons.users,
        InsightCategory.redemption => LucideIcons.ticketCheck,
        InsightCategory.catalog => LucideIcons.layoutGrid,
      };
}
