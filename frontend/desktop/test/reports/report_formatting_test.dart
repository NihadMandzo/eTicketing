import 'package:desktop/core/formatting.dart';
import 'package:flutter_test/flutter_test.dart';

/// The reports screen renders figures the server has already rounded, but the
/// Bosnian shaping of them happens here — and it has to match
/// `ReportFormatting` on the server, or the same number reads differently on
/// screen and in the exported PDF.
void main() {
  group('formatPercent', () {
    test('renders one decimal with a comma separator', () {
      expect(formatPercent(12.4), '12,4%');
      expect(formatPercent(100), '100,0%');
    });

    test('rounds to one decimal', () {
      expect(formatPercent(12.45), '12,5%');
      expect(formatPercent(0.04), '0,0%');
    });

    test('renders an em dash for a value that could not be computed', () {
      // A DailyEntry product's occupancy, growth with no preceding period, the
      // peak hour of an empty histogram — all legitimately null.
      expect(formatPercent(null), '—');
    });

    test('groups thousands for an implausibly large percentage', () {
      expect(formatPercent(1234.5), '1.234,5%');
    });
  });

  group('formatSignedPercent', () {
    test('prefixes a plus for growth', () {
      expect(formatSignedPercent(18.2), '+18,2%');
    });

    test('uses a typographic minus for a decline', () {
      expect(formatSignedPercent(-4.1), '−4,1%');
    });

    test('treats zero as non-negative', () {
      expect(formatSignedPercent(0), '+0,0%');
    });

    test('renders an em dash when there is nothing to compare against', () {
      expect(formatSignedPercent(null), '—');
    });
  });

  group('formatHourWindow', () {
    test('renders the hour and the one after it', () {
      expect(formatHourWindow(19), '19:00 – 20:00');
    });

    test('wraps around midnight', () {
      expect(formatHourWindow(23), '23:00 – 00:00');
    });

    test('pads a single-digit hour', () {
      expect(formatHourWindow(9), '09:00 – 10:00');
    });

    test('renders an em dash when nothing was scanned', () {
      expect(formatHourWindow(null), '—');
    });
  });

  group('formatLongDate', () {
    test('renders the Bosnian month name and a trailing dot', () {
      expect(formatLongDate(DateTime(2026, 8, 21)), '21. avgust 2026.');
      expect(formatLongDate(DateTime(2026, 1, 1)), '1. januar 2026.');
      expect(formatLongDate(DateTime(2026, 12, 31)), '31. decembar 2026.');
    });
  });

  group('formatMoney', () {
    test('groups thousands and keeps two decimals', () {
      // Already covered indirectly elsewhere, asserted here because every
      // figure on the reports screen goes through it.
      expect(formatMoney(1284600), '1.284.600,00 KM');
      expect(formatMoney(43.6), '43,60 KM');
    });
  });
}
