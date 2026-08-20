/// Mirrors `eTicketing.Contracts.TicketingMode` — drives how a Category's
/// Products/Sectors/Tickets behave (see [[01-domain]]). Shared by every
/// catalog/sector/ticket model below rather than re-declared per file.
enum TicketingMode { singleOccurrence, dailyEntry, recurringReservation }

TicketingMode ticketingModeFromJson(String value) => switch (value) {
      'DailyEntry' => TicketingMode.dailyEntry,
      'RecurringReservation' => TicketingMode.recurringReservation,
      _ => TicketingMode.singleOccurrence,
    };
