import '../ticketing_mode.dart';

/// One row of the organizer's "what am I validating today" list
/// (`GET /api/tickets/validation/products`). Counts cover the current calendar
/// day only — a museum with 400 day-passes sold across the month shows only
/// the ones admitting entry today.
class ValidationProductResponse {
  final String productId;
  final String name;
  final DateTime? date;
  final TicketingMode ticketingMode;
  final int totalToday;
  final int validatedToday;

  const ValidationProductResponse({
    required this.productId,
    required this.name,
    this.date,
    required this.ticketingMode,
    required this.totalToday,
    required this.validatedToday,
  });

  int get remainingToday => totalToday - validatedToday;

  factory ValidationProductResponse.fromJson(Map<String, dynamic> json) {
    return ValidationProductResponse(
      productId: json['productId'] as String,
      name: json['name'] as String? ?? '',
      date: json['date'] != null ? DateTime.tryParse(json['date'] as String) : null,
      ticketingMode: ticketingModeFromJson(json['ticketingMode']),
      totalToday: (json['totalToday'] as num?)?.toInt() ?? 0,
      validatedToday: (json['validatedToday'] as num?)?.toInt() ?? 0,
    );
  }
}
