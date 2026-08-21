class TicketTypeResponse {
  final String id;
  final String sectorId;
  final String name;
  final double price;
  final DateTime createdAt;

  const TicketTypeResponse({
    required this.id,
    required this.sectorId,
    required this.name,
    required this.price,
    required this.createdAt,
  });

  factory TicketTypeResponse.fromJson(Map<String, dynamic> json) {
    if (json['id'] == null) throw const FormatException('Missing id in payload');
    if (json['sectorId'] == null) throw const FormatException('Missing sectorId in payload');
    if (json['name'] == null) throw const FormatException('Missing name in payload');

    return TicketTypeResponse(
      id: json['id'] as String,
      sectorId: json['sectorId'] as String,
      name: json['name'] as String,
      price: (json['price'] as num?)?.toDouble() ?? 0,
      createdAt: DateTime.parse(json['createdAt'] as String),
    );
  }
}
