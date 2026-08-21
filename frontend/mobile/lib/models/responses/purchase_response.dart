import 'ticket_response.dart';

class PurchaseResponse {
  final String orderId;
  final String productId;
  final String sectorId;
  final double totalPaid;
  final DateTime purchasedAt;
  final List<TicketResponse> tickets;

  const PurchaseResponse({
    required this.orderId,
    required this.productId,
    required this.sectorId,
    required this.totalPaid,
    required this.purchasedAt,
    required this.tickets,
  });

  factory PurchaseResponse.fromJson(Map<String, dynamic> json) {
    if (json['orderId'] == null) throw const FormatException('Missing orderId in payload');
    if (json['productId'] == null) throw const FormatException('Missing productId in payload');
    if (json['sectorId'] == null) throw const FormatException('Missing sectorId in payload');
    if (json['totalPaid'] == null) throw const FormatException('Missing totalPaid in payload');

    return PurchaseResponse(
      orderId: json['orderId'] as String,
      productId: json['productId'] as String,
      sectorId: json['sectorId'] as String,
      totalPaid: (json['totalPaid'] as num).toDouble(),
      purchasedAt: DateTime.tryParse(json['purchasedAt'] as String? ?? '') ?? DateTime.now(),
      tickets: (json['tickets'] as List? ?? []).map((e) => TicketResponse.fromJson(e as Map<String, dynamic>)).toList(),
    );
  }
}
