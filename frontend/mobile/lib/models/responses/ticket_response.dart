import '../api_enum.dart';

class TicketResponse {
  final String id;
  final String orderId;
  final String sectorId;
  final String sectorName;
  final String productId;
  final String? ticketTypeId;
  final String? ticketTypeName;
  final String status;
  final double pricePaid;
  final DateTime? validDate;
  final DateTime? validFrom;
  final DateTime? validTo;
  final DateTime createdAt;

  /// The signed code this ticket's QR encodes — what an organizer's scanner
  /// reads back at the gate.
  final String qrPayload;

  /// Ready-to-render `data:image/png;base64,...` QR, produced server-side by
  /// eTicketing.Ticketing. Rendered there rather than here so this app and the
  /// Angular web app share one QR implementation and neither needs a QR
  /// package of its own.
  final String qrImage;

  const TicketResponse({
    required this.id,
    required this.orderId,
    required this.sectorId,
    required this.sectorName,
    required this.productId,
    this.ticketTypeId,
    this.ticketTypeName,
    required this.status,
    required this.pricePaid,
    this.validDate,
    this.validFrom,
    this.validTo,
    required this.createdAt,
    this.qrPayload = '',
    this.qrImage = '',
  });

  factory TicketResponse.fromJson(Map<String, dynamic> json) {
    if (json['id'] == null) throw const FormatException('Missing id in payload');
    if (json['orderId'] == null) throw const FormatException('Missing orderId in payload');
    if (json['sectorId'] == null) throw const FormatException('Missing sectorId in payload');
    if (json['productId'] == null) throw const FormatException('Missing productId in payload');

    return TicketResponse(
      id: json['id'] as String,
      orderId: json['orderId'] as String,
      sectorId: json['sectorId'] as String,
      sectorName: json['sectorName'] as String? ?? '',
      productId: json['productId'] as String,
      ticketTypeId: json['ticketTypeId'] as String?,
      ticketTypeName: json['ticketTypeName'] as String?,
      status: ticketStatusFromJson(json['status']),
      pricePaid: (json['pricePaid'] as num?)?.toDouble() ?? 0,
      validDate: json['validDate'] != null ? DateTime.tryParse(json['validDate'] as String) : null,
      validFrom: json['validFrom'] != null ? DateTime.tryParse(json['validFrom'] as String) : null,
      validTo: json['validTo'] != null ? DateTime.tryParse(json['validTo'] as String) : null,
      createdAt: DateTime.tryParse(json['createdAt'] as String? ?? '') ?? DateTime.now(),
      qrPayload: json['qrPayload'] as String? ?? '',
      qrImage: json['qrImage'] as String? ?? '',
    );
  }
}
