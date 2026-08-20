import 'api_enum.dart';

/// Mirrors `eTicketing.Contracts.TicketingMode` — drives how a Category's
/// Products/Sectors/Tickets behave (see [[01-domain]]). Shared by every
/// catalog/sector/ticket model below rather than re-declared per file.
enum TicketingMode { singleOccurrence, dailyEntry, recurringReservation }

/// Declaration order must match the backend enum exactly — the wire value is
/// the ordinal, not the name. See [enumNameFromJson].
const ticketingModeNames = ['SingleOccurrence', 'DailyEntry', 'RecurringReservation'];

TicketingMode ticketingModeFromJson(dynamic value) =>
    switch (enumNameFromJson(value, ticketingModeNames, 'SingleOccurrence')) {
      'DailyEntry' => TicketingMode.dailyEntry,
      'RecurringReservation' => TicketingMode.recurringReservation,
      _ => TicketingMode.singleOccurrence,
    };
