import '../api_enum.dart';
import '../ticketing_mode.dart';
import 'ticket_type_response.dart';

class SectorResponse {
  final String id;
  final String productId;
  final String name;
  final int capacity;
  final double price;
  final String status;
  final TicketingMode ticketingMode;
  final int? periodYear;
  final int? periodMonth;
  final DateTime createdAt;
  final List<TicketTypeResponse> ticketTypes;

  const SectorResponse({
    required this.id,
    required this.productId,
    required this.name,
    required this.capacity,
    required this.price,
    required this.status,
    required this.ticketingMode,
    this.periodYear,
    this.periodMonth,
    required this.createdAt,
    required this.ticketTypes,
  });

  factory SectorResponse.fromJson(Map<String, dynamic> json) {
    if (json['id'] == null) throw const FormatException('Missing id in payload');
    if (json['productId'] == null) throw const FormatException('Missing productId in payload');
    if (json['name'] == null) throw const FormatException('Missing name in payload');
    if (json['price'] == null) throw const FormatException('Missing price in payload');

    return SectorResponse(
      id: json['id'] as String,
      productId: json['productId'] as String,
      name: json['name'] as String,
      capacity: json['capacity'] as int? ?? 0,
      price: (json['price'] as num).toDouble(),
      status: publishStatusFromJson(json['status']),
      ticketingMode: ticketingModeFromJson(json['ticketingMode']),
      periodYear: json['periodYear'] as int?,
      periodMonth: json['periodMonth'] as int?,
      createdAt: DateTime.tryParse(json['createdAt'] as String? ?? '') ?? DateTime.now(),
      ticketTypes:
          (json['ticketTypes'] as List? ?? []).map((e) => TicketTypeResponse.fromJson(e as Map<String, dynamic>)).toList(),
    );
  }
}
