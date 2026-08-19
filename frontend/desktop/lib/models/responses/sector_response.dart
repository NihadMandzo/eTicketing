import '../enums/ticketing_mode.dart';

class SectorResponse {
  final String id;
  final String productId;
  final String name;
  final int capacity;
  final double price;
  final int status; // 0 = Draft, 1 = Published (PublishStatus, serialized as a plain int)
  final TicketingMode ticketingMode;
  final int? periodYear; // DailyEntry only
  final int? periodMonth; // DailyEntry only, 1-12
  final DateTime createdAt;

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
  });

  bool get isPublished => status == 1;

  factory SectorResponse.fromJson(Map<String, dynamic> json) {
    if (json['id'] == null) throw const FormatException('Missing id in payload');
    if (json['productId'] == null) throw const FormatException('Missing productId in payload');
    if (json['name'] == null) throw const FormatException('Missing name in payload');
    if (json['createdAt'] == null) throw const FormatException('Missing createdAt in payload');

    return SectorResponse(
      id: json['id'] as String,
      productId: json['productId'] as String,
      name: json['name'] as String,
      capacity: json['capacity'] as int? ?? 0,
      price: (json['price'] as num?)?.toDouble() ?? 0,
      status: json['status'] as int? ?? 0,
      ticketingMode: TicketingMode.fromValue(json['ticketingMode'] as int? ?? 0),
      periodYear: json['periodYear'] as int?,
      periodMonth: json['periodMonth'] as int?,
      createdAt: DateTime.parse(json['createdAt'] as String),
    );
  }
}
