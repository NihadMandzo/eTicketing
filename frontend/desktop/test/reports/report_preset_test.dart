import 'package:desktop/screens/widgets/reports/report_range_bar.dart';
import 'package:flutter_test/flutter_test.dart';

/// The period shortcuts are the one piece of real date arithmetic on the
/// reports screen: they decide the range every request is made for, and a
/// day-count that is off by one silently shifts every figure.
void main() {
  final today = DateTime(2026, 8, 21);

  ReportPreset presetById(String id) => ReportPreset.all.firstWhere((p) => p.id == id);

  int inclusiveDays(DateTime from, DateTime to) => to.difference(from).inDays + 1;

  group('day-based presets', () {
    test('7 dana covers today and the six days before it', () {
      final (from, to) = presetById('7d').resolve(today);

      expect(to, DateTime(2026, 8, 21));
      expect(from, DateTime(2026, 8, 15));
      expect(inclusiveDays(from, to), 7);
    });

    test('30 dana counts inclusively, not as a 30-day offset', () {
      final (from, to) = presetById('30d').resolve(today);

      expect(from, DateTime(2026, 7, 23));
      expect(inclusiveDays(from, to), 30);
    });

    test('90 dana counts inclusively', () {
      final (from, to) = presetById('90d').resolve(today);

      expect(inclusiveDays(from, to), 90);
    });
  });

  group('month-based presets', () {
    test('3 mjeseca lands on the same day three calendar months back', () {
      // Deliberately not "90 days": a user asking for three months means three
      // months, and the two answers differ by up to two days.
      final (from, to) = presetById('3m').resolve(today);

      expect(from, DateTime(2026, 5, 21));
      expect(to, DateTime(2026, 8, 21));
    });

    test('6 mjeseci crosses into the previous half of the year', () {
      final (from, _) = presetById('6m').resolve(today);

      expect(from, DateTime(2026, 2, 21));
    });

    test('Godina lands on the same date a year earlier', () {
      final (from, to) = presetById('1y').resolve(today);

      expect(from, DateTime(2025, 8, 21));
      // 2025-08-21 .. 2026-08-21 inclusive is 366 days — exactly the API's cap,
      // so the longest preset is still an accepted range rather than a 400.
      expect(inclusiveDays(from, to), ReportRangeBar.maxRangeDays);
    });
  });

  group('boundaries', () {
    test('drops the time component so a range never depends on the hour', () {
      final (from, to) = presetById('7d').resolve(DateTime(2026, 8, 21, 23, 45));

      expect(to.hour, 0);
      expect(from.hour, 0);
    });

    test('rolls a month-based preset back across a year boundary', () {
      final (from, _) = presetById('3m').resolve(DateTime(2026, 1, 15));

      expect(from, DateTime(2025, 10, 15));
    });

    test('every preset produces a range within the API cap', () {
      for (final preset in ReportPreset.all) {
        final (from, to) = preset.resolve(today);
        expect(
          inclusiveDays(from, to),
          lessThanOrEqualTo(ReportRangeBar.maxRangeDays),
          reason: 'preset ${preset.id} exceeds the range the API accepts',
        );
        expect(from.isAfter(to), isFalse, reason: 'preset ${preset.id} is inverted');
      }
    });
  });
}
