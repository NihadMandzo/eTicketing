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
