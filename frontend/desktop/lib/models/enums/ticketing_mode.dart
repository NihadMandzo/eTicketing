/// Mirrors the backend's `eTicketing.Contracts.Persistence.TicketingMode` ordinals exactly
/// (SingleOccurrence(0), DailyEntry(1), RecurringReservation(2) — see .claude/rules/01-domain.md).
/// Governs how a Category's Products/Sectors behave; raw-numeric JSON convention, same as
/// RoleType.
enum TicketingMode {
  singleOccurrence(0),
  dailyEntry(1),
  recurringReservation(2);

  final int value;
  const TicketingMode(this.value);

  static TicketingMode fromValue(int value) => TicketingMode.values.firstWhere(
        (m) => m.value == value,
        orElse: () => TicketingMode.singleOccurrence,
      );

  /// Bosnian label for UI display (dropdowns, badges, info banners).
  String get label => switch (this) {
        TicketingMode.singleOccurrence => 'Jednokratni događaj',
        TicketingMode.dailyEntry => 'Dnevna ulaznica',
        TicketingMode.recurringReservation => 'Mjesečna rezervacija',
      };

  /// Short explanation shown in mode-aware info banners on Product/Sector forms.
  String get description => switch (this) {
        TicketingMode.singleOccurrence =>
          'Klasičan događaj sa jednim tačnim datumom/vremenom. Sektor predstavlja sekciju sjedišta (npr. "VIP") sa fiksnim kapacitetom.',
        TicketingMode.dailyEntry =>
          'Npr. muzej ili zoo. Sektor definiše kapacitet i cijenu po danu za cijeli mjesec — kupac kasnije bira tačan datum.',
        TicketingMode.recurringReservation =>
          'Npr. mjesečni parking. Sektor predstavlja jedno konkretno označeno mjesto — kapacitet je uvijek 1.',
      };
}
