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

  group('end-of-month subtraction', () {
    // DateTime normalises an out-of-range day instead of rejecting it, so a naive
    // DateTime(y, m - 3, 31) for 31 May becomes "31 February" = 3 March: the wrong
    // month, and two days short of the range the user asked for.
    test('3 mjeseca from 31 May clamps to the last day of February', () {
      final (from, to) = presetById('3m').resolve(DateTime(2026, 5, 31));

      expect(from, DateTime(2026, 2, 28));
      expect(to, DateTime(2026, 5, 31));
      expect(from.month, 2, reason: 'must not spill over into March');
    });

    test('1 mjesec-equivalent subtraction never leaves the target month', () {
      // 31 March minus one month is 28 February 2026, not 3 March.
      final (from, _) = ReportPreset('1m', '1 mjesec', months: 1).resolve(DateTime(2026, 3, 31));

      expect(from, DateTime(2026, 2, 28));
    });

    test('clamps to 29 February in a leap year', () {
      final (from, _) = presetById('3m').resolve(DateTime(2024, 5, 31));

      expect(from, DateTime(2024, 2, 29));
    });

    test('keeps the day when the target month is long enough', () {
      final (from, _) = presetById('3m').resolve(DateTime(2026, 5, 30));

      expect(from, DateTime(2026, 2, 28), reason: 'February still clamps');

      final (marchFrom, _) = presetById('3m').resolve(DateTime(2026, 6, 30));
      expect(marchFrom, DateTime(2026, 3, 30), reason: 'March has a 30th, so no clamping');
    });

    test('Godina clamps forward when the preceding year contains a leap day', () {
      // 2023-05-31 .. 2024-05-31 is 367 days inclusive, because 29 Feb 2024
      // falls inside it — one past the cap, so unclamped the preset would send
      // a request the API refuses and the user would see the "period too long"
      // banner instead of a year of data.
      final (from, to) = presetById('1y').resolve(DateTime(2024, 5, 31));

      expect(inclusiveDays(from, to), ReportRangeBar.maxRangeDays);
      expect(from, DateTime(2023, 6, 1), reason: 'one day later than the naive 12-month subtraction');
    });

    test('Godina is left alone when the preceding year has no leap day', () {
      final (from, to) = presetById('1y').resolve(DateTime(2026, 5, 31));

      expect(from, DateTime(2025, 5, 31), reason: 'already 366 days, so no clamping');
      expect(inclusiveDays(from, to), ReportRangeBar.maxRangeDays);
    });

    test('a clamped range is still within the API cap and never inverted', () {
      final dates = [
        DateTime(2026, 5, 31), DateTime(2026, 3, 31), DateTime(2026, 1, 31),
        DateTime(2024, 5, 31), DateTime(2024, 3, 1), DateTime(2025, 1, 1),
        DateTime(2024, 2, 29), DateTime(2027, 12, 31),
      ];
      for (final today in dates) {
        for (final preset in ReportPreset.all) {
          final (from, to) = preset.resolve(today);
          expect(from.isAfter(to), isFalse, reason: '${preset.id} on $today is inverted');
          expect(inclusiveDays(from, to), lessThanOrEqualTo(ReportRangeBar.maxRangeDays));
        }
      }
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
