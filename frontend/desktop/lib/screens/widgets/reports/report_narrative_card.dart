import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../../theme/app_colors.dart';

/// The generated executive summary on the AI Uvidi tab.
///
/// Labelled as machine-written rather than slipped in as the platform's own
/// words — a reader deciding how much to trust a sentence needs to know what
/// wrote it. The whole card is absent when no model is configured or the call
/// failed (`INarrativeWriter` returns null, see .claude/rules/01-domain.md).
///
/// Set in columns once there is room for them. The summary is one long
/// paragraph, and on a maximised 1920px window a single full-width block runs
/// past 250 characters a line — far outside the measure a reader can track back
/// from, so the eye loses its place on every return sweep. Columns of roughly
/// eighty characters are the typographic answer, and they use the width instead
/// of merely spanning it.
class ReportNarrativeCard extends StatelessWidget {
  final String narrative;

  const ReportNarrativeCard({super.key, required this.narrative});

  /// The measure each column aims for: about 80 characters at the 13.5px body
  /// size below. Columns are only added while every one of them can keep it.
  static const double _targetColumnWidth = 520;

  static const double _columnGap = 32;

  /// Three is the ceiling. A fourth column at 1920px would still measure well,
  /// but a summary chopped into four short stacks reads as a layout exercise
  /// rather than a paragraph.
  static const int _maxColumns = 3;

  @override
  Widget build(BuildContext context) {
    final brightness = Theme.of(context).brightness;
    final isDark = brightness == Brightness.dark;

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: isDark
            ? AppColors.secondary.withValues(alpha: 0.10)
            : AppColors.secondary.withValues(alpha: 0.07),
        border: Border.all(color: AppColors.secondary.withValues(alpha: 0.45)),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(LucideIcons.sparkles, size: 16, color: AppColors.secondary),
              const SizedBox(width: 8),
              Text(
                'AI SAŽETAK',
                style: TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.w800,
                  letterSpacing: 0.8,
                  color: AppColors.secondary,
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          LayoutBuilder(
            builder: (context, constraints) {
              final columns = columnsFor(constraints.maxWidth);
              final parts = balanceIntoColumns(narrative, columns);
              if (parts.length == 1) return _column(parts.first, brightness);

              return Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  for (var i = 0; i < parts.length; i++) ...[
                    if (i > 0) const SizedBox(width: _columnGap),
                    Expanded(child: _column(parts[i], brightness)),
                  ],
                ],
              );
            },
          ),
        ],
      ),
    );
  }

  Widget _column(String text, Brightness brightness) => Text(
        text,
        style: TextStyle(
          fontSize: 13.5,
          height: 1.55,
          color: AppColors.textSecondary(brightness),
        ),
      );

  /// How many columns [width] can hold while each still measures about
  /// [_targetColumnWidth]. Visible for testing.
  @visibleForTesting
  static int columnsFor(double width) {
    final fit = ((width + _columnGap) / (_targetColumnWidth + _columnGap)).floor();
    return fit.clamp(1, _maxColumns);
  }

  /// Splits [text] into [columns] chunks of roughly equal length.
  ///
  /// Breaks only between sentences — a column starting mid-clause would read
  /// worse than the over-long line this is fixing — and never drops or
  /// truncates anything: when the text has fewer sentences than requested
  /// columns it comes back whole, as a single chunk, and the caller lays it out
  /// full width. Visible for testing.
  @visibleForTesting
  static List<String> balanceIntoColumns(String text, int columns) {
    if (columns <= 1) return [text];

    final sentences = [
      for (final match in RegExp(r'[^.!?]+[.!?]*\s*').allMatches(text))
        if (match.group(0)!.trim().isNotEmpty) match.group(0)!,
    ];
    if (sentences.length < columns) return [text];

    final target = text.length / columns;
    final result = <String>[];
    var buffer = StringBuffer();

    for (var i = 0; i < sentences.length; i++) {
      buffer.write(sentences[i]);

      final columnsLeft = columns - result.length - 1;
      final sentencesLeft = sentences.length - i - 1;
      // Close this column once it has its share — or earlier, the moment there
      // are only just enough sentences left to give every remaining column one.
      // Without that second condition a long opening sentence could swallow the
      // text and leave the last column empty.
      final shouldClose =
          columnsLeft > 0 && (buffer.length >= target || sentencesLeft <= columnsLeft);
      if (shouldClose) {
        result.add(buffer.toString().trimRight());
        buffer = StringBuffer();
      }
    }
    result.add(buffer.toString().trimRight());

    return result;
  }
}
