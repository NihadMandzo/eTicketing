/// Bosnian number, money and date formatting.
///
/// Written out rather than pulled from `package:intl`: the app has no locale
/// data loaded, and these are the only four shapes the back-office needs.
library;

/// A dot every three digits, so `93600` reads `93.600`.
String formatCount(int value) {
  final digits = value.abs().toString();
  final buffer = StringBuffer(value < 0 ? '-' : '');

  for (var i = 0; i < digits.length; i++) {
    if (i > 0 && (digits.length - i) % 3 == 0) buffer.write('.');
    buffer.write(digits[i]);
  }

  return buffer.toString();
}

/// Grouped thousands, comma decimals — `93.600,00 KM`. Matches
/// `PrintSheetDocument.Money`, so a price reads the same on screen and on paper.
String formatMoney(double amount) {
  final cents = (amount * 100).round();
  return '${formatCount(cents ~/ 100)},${(cents % 100).toString().padLeft(2, '0')} KM';
}

/// `26.08.2026.` — the trailing dot is part of the Bosnian convention.
String formatDate(DateTime date) =>
    '${date.day.toString().padLeft(2, '0')}.${date.month.toString().padLeft(2, '0')}.${date.year}.';

String formatTime(DateTime date) =>
    '${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}';

/// The human-readable number printed on a physical ticket's stub, six digits
/// wide — `#000482`. Mirrors `PrintTicketModel.StubNumber`.
String formatStubNumber(int serial) => '#${serial.toString().padLeft(6, '0')}';

/// `12,4%` — one decimal, comma separator. `null` becomes an em dash, which is
/// how every "we cannot compute this" case in the reports renders (a DailyEntry
/// product's occupancy, growth with no preceding period, an empty histogram's
/// peak hour). Mirrors `ReportFormatting.Percent` on the server, so the screen
/// and the exported PDF read identically.
String formatPercent(double? value) {
  if (value == null) return '—';
  final tenths = (value.abs() * 10).round();
  final sign = value < 0 ? '-' : '';
  return '$sign${formatCount(tenths ~/ 10)},${tenths % 10}%';
}

/// `+18,2%` / `−4,1%` — period-over-period change. Uses the typographic minus
/// (U+2212) rather than a hyphen, matching the design. Mirrors
/// `ReportFormatting.SignedPercent`.
String formatSignedPercent(double? value) {
  if (value == null) return '—';
  final magnitude = formatPercent(value.abs());
  return value < 0 ? '−$magnitude' : '+$magnitude';
}

/// `19:00 – 20:00` — the busiest arrival hour, as a window rather than an
/// instant. Mirrors `ReportFormatting.HourWindow`.
String formatHourWindow(int? hour) {
  if (hour == null) return '—';
  final next = (hour + 1) % 24;
  return '${hour.toString().padLeft(2, '0')}:00 – ${next.toString().padLeft(2, '0')}:00';
}

/// `23. juli 2026.` — the long form the report header uses, next to the short
/// numeric [formatDate] used in tables.
String formatLongDate(DateTime date) => '${date.day}. ${_months[date.month - 1]} ${date.year}.';

const _months = [
  'januar', 'februar', 'mart', 'april', 'maj', 'juni',
  'juli', 'avgust', 'septembar', 'oktobar', 'novembar', 'decembar',
];
