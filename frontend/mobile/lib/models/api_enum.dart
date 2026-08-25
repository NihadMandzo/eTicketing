/// The backend serializes every C# enum with System.Text.Json's **default**
/// converter, which writes the integer ordinal — `"ticketingMode": 0`,
/// `"status": 1` — not the name. (`eTicketing.Contracts` deliberately keeps
/// it that way; the Flutter desktop client already parses those ordinals,
/// see `desktop/lib/models/enums/ticketing_mode.dart`.)
///
/// Dart's `json['status'] as String?` blows up on an int rather than
/// falling back, which is how a perfectly successful
/// `GET /api/products/{id}` used to surface as "Događaj nije pronađen ili
/// više nije dostupan." — the TypeError was swallowed by the screen's
/// generic `catch`.
///
/// [enumNameFromJson] maps whatever arrives (ordinal, ordinal-as-string, or
/// the name itself) onto the backend's declared name. [names] must list the
/// values in the backend's declaration order.
String enumNameFromJson(dynamic value, List<String> names, String fallback) {
  if (value is int) {
    return value >= 0 && value < names.length ? names[value] : fallback;
  }
  if (value is String) {
    // A numeric string is what an ordinal survives as through a
    // query-string or form round-trip.
    final ordinal = int.tryParse(value);
    if (ordinal != null) {
      return ordinal >= 0 && ordinal < names.length ? names[ordinal] : fallback;
    }
    return names.contains(value) ? value : fallback;
  }
  return fallback;
}

/// Mirrors `eTicketing.Contracts.Persistence.PublishStatus`.
const publishStatusNames = ['Draft', 'Published'];

/// Mirrors `eTicketing.Ticketing.Data.Entities.TicketStatus`. 'Used' is
/// appended (never inserted) because the wire value is the ordinal — it's the
/// terminal status a ticket reaches once an organizer scans it at the gate.
const ticketStatusNames = [
  'Processing',
  'Confirmed',
  'Ready',
  'Cancelled',
  'Used',
];

String publishStatusFromJson(dynamic value) => enumNameFromJson(value, publishStatusNames, 'Published');

String ticketStatusFromJson(dynamic value) => enumNameFromJson(value, ticketStatusNames, 'Confirmed');
