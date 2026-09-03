import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../../theme/app_colors.dart';
import 'report_range_bar.dart';

/// How far past the selected period the AI Uvidi tab projects.
///
/// Months, not days. An organizer plans a season — the question they open this
/// tab with is "how does the next quarter look", never "how do the next
/// fourteen days look" — and the four values mirror
/// `InsightsQueryValidator.AllowedHorizons` exactly: the API refuses anything
/// else, so a fifth pill here would produce a 400 rather than a longer forecast.
///
/// [phrase] is the Bosnian wording, duplicated from the backend's
/// `HorizonLabel` so a label like "narednih 6 mjeseci" reads identically whether
/// this screen composed it or the server did. Asking the server for the word
/// "six months" would be worse than keeping the two tables in step.
class ReportHorizon {
  /// What travels on the wire: `?horizon=` in days.
  final int days;

  /// The pill's own text.
  final String label;

  /// The period in the genitive, to follow "narednih"/"prethodnih".
  final String phrase;

  const ReportHorizon(this.days, this.label, this.phrase);

  static const all = [
    ReportHorizon(30, '1 mjesec', 'mjesec dana'),
    ReportHorizon(90, '3 mjeseca', '3 mjeseca'),
    ReportHorizon(180, '6 mjeseci', '6 mjeseci'),
    ReportHorizon(365, '1 godina', 'godinu dana'),
  ];

  /// The horizon the tab opens on — the shortest, and the one the PDF export
  /// uses (`ReportPdfService.DefaultExportHorizon`), so an export matches the
  /// tab as it first renders.
  static const defaultDays = 30;

  /// "6 mjeseci". Falls back to a bare day count for a horizon this table does
  /// not know, which the API cannot currently return but a future one might.
  static String phraseFor(int days) {
    for (final horizon in all) {
      if (horizon.days == days) return horizon.phrase;
    }
    return '$days dana';
  }

  /// "narednih 6 mjeseci" — what the projection covers.
  static String next(int days) => 'narednih ${phraseFor(days)}';

  /// "prethodnih 6 mjeseci" — the equally long stretch behind it that the
  /// change percentage compares against.
  static String previous(int days) => 'prethodnih ${phraseFor(days)}';
}

/// The projection selector: one strip at the top of the AI Uvidi tab.
///
/// Deliberately not inside the Prognoza prihoda card and deliberately not in the
/// period bar above the tab strip. Inside the chart it looked like a setting for
/// one chart, when in fact the two headline tiles, several of the findings and
/// the generated summary all change with it. In the period bar it sat against
/// that bar's own "7 dana / 30 dana / 90 dana" date presets — the same-looking
/// control twice on one screen, one choosing which history is analysed and the
/// other how far past it to project.
///
/// Its own strip, directly above the blocks it governs, is what makes the scope
/// legible: everything below this line is projected this far.
class ReportHorizonBar extends StatelessWidget {
  final int selectedDays;

  /// Null while the tab is loading — the whole strip goes unpressable rather
  /// than queueing a second request behind the first.
  final ValueChanged<ReportHorizon>? onSelect;

  /// True while a horizon change is in flight. The selected pill shows a
  /// spinner; the tab keeps its current figures on screen (see
  /// `_changeHorizon`), which is the whole reason this is not the tab's own
  /// loading flag.
  final bool isRefreshing;

  const ReportHorizonBar({
    super.key,
    required this.selectedDays,
    required this.onSelect,
    this.isRefreshing = false,
  });

  @override
  Widget build(BuildContext context) {
    final brightness = Theme.of(context).brightness;
    final isDark = brightness == Brightness.dark;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurface : Colors.white,
        border: Border.all(color: AppColors.border(brightness)),
        borderRadius: BorderRadius.circular(12),
      ),
      // Wrap, not Row: at ~700px the four pills cannot share a line with their
      // label and the caption (.claude/rules/21-frontend-desktop.md).
      child: Wrap(
        crossAxisAlignment: WrapCrossAlignment.center,
        spacing: 10,
        runSpacing: 8,
        children: [
          Icon(LucideIcons.trendingUp, size: 16, color: AppColors.textTertiary(brightness)),
          Text(
            'Projekcija:',
            style: TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w600,
              color: AppColors.textTertiary(brightness),
            ),
          ),
          for (final horizon in ReportHorizon.all)
            ReportPeriodPill(
              label: horizon.label,
              selected: horizon.days == selectedDays,
              busy: isRefreshing && horizon.days == selectedDays,
              onTap: onSelect == null || isRefreshing || horizon.days == selectedDays
                  ? null
                  : () => onSelect!(horizon),
            ),
          // States the scope in words, because the strip sits under a period bar
          // that also holds pills: that one picks the history being analysed,
          // this one how far past it everything below projects.
          Text(
            'koliko unaprijed se projicira prodaja',
            style: TextStyle(fontSize: 12, color: AppColors.textTertiary(brightness)),
          ),
        ],
      ),
    );
  }
}
