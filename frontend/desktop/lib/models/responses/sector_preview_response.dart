import '../enums/ticketing_mode.dart';

/// Mirrors the backend's SectorPreviewResponse — returned by POST /sectors/preview.
class SectorPreviewResponse {
  final String productId;
  final String name;
  final int capacity;
  final double price;
  final TicketingMode ticketingMode;
  final int? periodYear;
  final int? periodMonth;

  const SectorPreviewResponse({
    required this.productId,
    required this.name,
    required this.capacity,
    required this.price,
    required this.ticketingMode,
    this.periodYear,
    this.periodMonth,
  });

  factory SectorPreviewResponse.fromJson(Map<String, dynamic> json) => SectorPreviewResponse(
        productId: json['productId'] as String? ?? '',
        name: json['name'] as String? ?? '',
        capacity: json['capacity'] as int? ?? 0,
        price: (json['price'] as num?)?.toDouble() ?? 0,
        ticketingMode: TicketingMode.fromValue(json['ticketingMode'] as int? ?? 0),
        periodYear: json['periodYear'] as int?,
        periodMonth: json['periodMonth'] as int?,
      );
}
