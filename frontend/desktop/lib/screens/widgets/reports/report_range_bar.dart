import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../../core/formatting.dart';
import '../../../theme/app_colors.dart';

/// A named shortcut in the period bar. `days` counts inclusively (7 dana =
/// today and the six before it); `months` steps back by calendar month, so
/// "3 mjeseca" lands on the same day-of-month rather than on day 90.
class ReportPreset {
  final String id;
  final String label;
  final int? days;
  final int? months;

  const ReportPreset(this.id, this.label, {this.days, this.months});

  /// The six shortcuts from docs/Design/Reports.dc.html.
  static const all = [
    ReportPreset('7d', '7 dana', days: 7),
    ReportPreset('30d', '30 dana', days: 30),
    ReportPreset('90d', '90 dana', days: 90),
    ReportPreset('3m', '3 mjeseca', months: 3),
    ReportPreset('6m', '6 mjeseci', months: 6),
    ReportPreset('1y', 'Godina', months: 12),
  ];

  /// The range this preset means, ending today.
  (DateTime from, DateTime to) resolve(DateTime today) {
    final to = DateTime(today.year, today.month, today.day);
    var from = months != null
        ? _monthsBefore(to, months!)
        // Day arithmetic through the constructor, not `subtract(Duration)`:
        // Duration is absolute time, so subtracting one across a DST change
        // lands on 23:00 or 01:00 of the intended day rather than midnight.
        // DateTime normalises an out-of-range day and keeps the wall clock.
        : DateTime(to.year, to.month, to.day - (days! - 1));

    // A calendar year is not always inside the API's limit. "Godina" from a
    // date whose preceding 12 months contain a leap day spans 367 days — one
    // past ReportRangeValidator.MaxRangeDays — and the report would answer 400
    // and render the "period is too long" banner instead of any data. That is
    // roughly one year in every four, for the whole year.
    //
    // Clamping forward keeps the preset usable and errs toward showing slightly
    // less than a full year rather than nothing at all.
    if (to.difference(from).inDays + 1 > ReportRangeBar.maxRangeDays) {
      from = DateTime(to.year, to.month, to.day - (ReportRangeBar.maxRangeDays - 1));
    }

    return (from, to);
  }

  /// [count] calendar months before [date], clamped to the target month's last
  /// day.
  ///
  /// The clamp is the whole point. `DateTime` normalises an out-of-range day
  /// instead of rejecting it, so the obvious `DateTime(y, m - count, d)` turns
  /// 31 May minus three months into 31 February — which becomes 3 March, a date
  /// in the *wrong month* and two days short of the range the user asked for.
  static DateTime _monthsBefore(DateTime date, int count) {
    // Day 0 of the following month is the last day of the month itself.
    final lastDayOfTargetMonth = DateTime(date.year, date.month - count + 1, 0).day;
    return DateTime(
      date.year,
      date.month - count,
      date.day < lastDayOfTargetMonth ? date.day : lastDayOfTargetMonth,
    );
  }
}

/// The date-range bar: preset pills on top, explicit Od/Do pickers underneath,
/// and the inline warning the design specifies for an inverted range.
class ReportRangeBar extends StatelessWidget {
  final DateTime from;
  final DateTime to;

  /// Null once the user picks explicit dates — no pill is highlighted then.
  final String? activePresetId;

  /// Today, in the app's own clock. The pickers refuse anything later: the API
  /// rejects a future `Do` outright, and offering it would only produce a 400.
  final DateTime today;

  final ValueChanged<ReportPreset> onPreset;
  final ValueChanged<DateTime> onFrom;
  final ValueChanged<DateTime> onTo;

  /// Pinned to the right edge of the date row — the export button, in practice.
  /// Passed in rather than built here so this widget stays about picking a
  /// period and knows nothing about PDFs or who is allowed to download one.
  final Widget? trailing;

  const ReportRangeBar({
    super.key,
    required this.from,
    required this.to,
    required this.activePresetId,
    required this.today,
    required this.onPreset,
    required this.onFrom,
    required this.onTo,
    this.trailing,
  });

  /// The longest range the API will accept, inclusive of both endpoints —
  /// mirrors `ReportRangeValidator.MaxRangeDays`. The backend re-checks it
  /// regardless (.claude/rules/21-frontend-desktop.md); this copy exists so the
  /// user sees why the report stopped updating instead of a 400.
  static const maxRangeDays = 366;

  bool get _isInverted => from.isAfter(to);

  bool get _isTooLong => !_isInverted && _days > maxRangeDays;

  bool get _isInvalid => _isInverted || _isTooLong;

  int get _days => to.difference(from).inDays + 1;

  @override
  Widget build(BuildContext context) {
    final brightness = Theme.of(context).brightness;
    final isDark = brightness == Brightness.dark;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurface : Colors.white,
        border: Border.all(color: AppColors.border(brightness)),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Wrap throughout, not Row: at ~700px the six pills cannot share a
          // line with their label, and a Row would overflow rather than reflow
          // (.claude/rules/21-frontend-desktop.md).
          Wrap(
            crossAxisAlignment: WrapCrossAlignment.center,
            spacing: 10,
            runSpacing: 8,
            children: [
              Icon(LucideIcons.calendarRange, size: 16, color: AppColors.textTertiary(brightness)),
              Text(
                'Period:',
                style: TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w600,
                  color: AppColors.textTertiary(brightness),
                ),
              ),
              for (final preset in ReportPreset.all)
                _PresetPill(
                  label: preset.label,
                  selected: preset.id == activePresetId,
                  onTap: () => onPreset(preset),
                ),
            ],
          ),
          const SizedBox(height: 12),
          Divider(height: 1, color: isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted),
          const SizedBox(height: 12),
          // Row wrapping a Wrap, rather than one flat Wrap: a Wrap cannot push a
          // single child to the far end, and `trailing` has to sit against the
          // card's right edge. The date controls keep reflowing inside the
          // Expanded half, so a narrow window still costs nothing.
          Row(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              Expanded(
                child: Wrap(
                  crossAxisAlignment: WrapCrossAlignment.center,
                  spacing: 10,
                  runSpacing: 10,
                  children: [
                    _DateField(
                      label: 'Od',
                      value: from,
                      // The picker itself enforces the ordering the API validates:
                      // "Od" can never be dragged past "Do".
                      lastDate: to,
                      onChanged: onFrom,
                    ),
                    _DateField(label: 'Do', value: to, firstDate: from, lastDate: today, onChanged: onTo),
                    Text(
                      '${formatLongDate(from)} – ${formatLongDate(to)}',
                      style: TextStyle(fontSize: 12, color: AppColors.textTertiary(brightness)),
                    ),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 11, vertical: 5),
                      decoration: BoxDecoration(
                        color: AppColors.primary.withValues(alpha: isDark ? 0.2 : 0.1),
                        borderRadius: BorderRadius.circular(20),
                      ),
                      child: Text(
                        _isInverted ? 'nevažeći raspon' : '$_days ${_days == 1 ? 'dan' : 'dana'}',
                        style: TextStyle(
                          fontSize: 11,
                          fontWeight: FontWeight.w700,
                          color: isDark ? AppColors.accent : AppColors.primary,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
              if (trailing != null) ...[const SizedBox(width: 12), trailing!],
            ],
          ),
          if (_isInvalid) ...[
            const SizedBox(height: 12),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 9),
              decoration: BoxDecoration(
                color: isDark ? AppColors.errorBgDarkMode : AppColors.errorBg,
                border: Border.all(color: isDark ? AppColors.errorDarkest : AppColors.errorBorder),
                borderRadius: BorderRadius.circular(10),
              ),
              child: Row(
                children: [
                  Icon(LucideIcons.circleAlert, size: 14, color: AppColors.error),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      _isInverted
                          ? 'Datum "Od" mora biti prije datuma "Do".'
                          : 'Period ne može biti duži od $maxRangeDays dana.',
                      style: TextStyle(fontSize: 12, color: isDark ? AppColors.error : AppColors.errorText),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _PresetPill extends StatelessWidget {
  final String label;
  final bool selected;
  final VoidCallback onTap;

  const _PresetPill({required this.label, required this.selected, required this.onTap});

  @override
  Widget build(BuildContext context) {
    final brightness = Theme.of(context).brightness;

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(20),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 13, vertical: 6),
        decoration: BoxDecoration(
          color: selected ? AppColors.primary : Colors.transparent,
          border: Border.all(color: selected ? AppColors.primary : AppColors.border(brightness)),
          borderRadius: BorderRadius.circular(20),
        ),
        child: Text(
          label,
          style: TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w600,
            color: selected ? Colors.white : AppColors.textTertiary(brightness),
          ),
        ),
      ),
    );
  }
}

class _DateField extends StatelessWidget {
  final String label;
  final DateTime value;
  final DateTime? firstDate;
  final DateTime? lastDate;
  final ValueChanged<DateTime> onChanged;

  const _DateField({
    required this.label,
    required this.value,
    this.firstDate,
    this.lastDate,
    required this.onChanged,
  });

  Future<void> _pick(BuildContext context) async {
    // The floor is deliberately generous rather than tied to the API's 366-day
    // cap: the cap is about the span, not about how far back a date may sit,
    // and a hard floor here would make a report on last year's festival
    // unpickable.
    final earliest = DateTime(value.year - 5);
    final picked = await showDatePicker(
      context: context,
      initialDate: value,
      firstDate: firstDate ?? earliest,
      lastDate: lastDate ?? DateTime(value.year + 1),
      // No `locale:` — the app registers no localization delegates, so asking
      // for 'bs' would throw. The picker's own chrome stays English; the labels
      // around it are Bosnian, same as ticket_export_screen.dart's picker.
      helpText: 'Odaberite datum',
      cancelText: 'Odustani',
      confirmText: 'Potvrdi',
    );
    if (picked != null) onChanged(DateTime(picked.year, picked.month, picked.day));
  }

  @override
  Widget build(BuildContext context) {
    final brightness = Theme.of(context).brightness;
    final isDark = brightness == Brightness.dark;

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(
          label,
          style: TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w600,
            color: AppColors.textTertiary(brightness),
          ),
        ),
        const SizedBox(width: 7),
        InkWell(
          onTap: () => _pick(context),
          borderRadius: BorderRadius.circular(10),
          child: Container(
            height: 36,
            padding: const EdgeInsets.symmetric(horizontal: 12),
            decoration: BoxDecoration(
              color: isDark ? AppColors.darkInputFill : AppColors.lightInputFill,
              border: Border.all(color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
              borderRadius: BorderRadius.circular(10),
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(LucideIcons.calendar, size: 14, color: AppColors.textTertiary(brightness)),
                const SizedBox(width: 7),
                Text(
                  formatDate(value),
                  style: TextStyle(fontSize: 13, color: AppColors.textPrimary(brightness)),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

/// The underlined tab strip above the report body.
class ReportTabBar extends StatelessWidget {
  final List<String> labels;
  final int selectedIndex;
  final ValueChanged<int> onSelect;

  const ReportTabBar({super.key, required this.labels, required this.selectedIndex, required this.onSelect});

  @override
  Widget build(BuildContext context) {
    final brightness = Theme.of(context).brightness;
    final isDark = brightness == Brightness.dark;
    final active = isDark ? AppColors.accent : AppColors.primary;

    return Container(
      decoration: BoxDecoration(
        border: Border(bottom: BorderSide(color: AppColors.border(brightness))),
      ),
      // Scrolls rather than wraps: four tabs at a narrow width would otherwise
      // spill onto a second row and detach the underline from the strip.
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: Row(
          children: [
            for (var i = 0; i < labels.length; i++)
              InkWell(
                onTap: () => onSelect(i),
                child: Container(
                  padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                  decoration: BoxDecoration(
                    border: Border(
                      bottom: BorderSide(color: i == selectedIndex ? active : Colors.transparent, width: 2),
                    ),
                  ),
                  child: Text(
                    labels[i],
                    style: TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w600,
                      color: i == selectedIndex ? active : AppColors.textTertiary(brightness),
                    ),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}
