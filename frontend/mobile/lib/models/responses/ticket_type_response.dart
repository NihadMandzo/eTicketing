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
    return TicketTypeResponse(
      id: json['id'] as String,
      sectorId: json['sectorId'] as String,
      name: json['name'] as String,
      price: (json['price'] as num).toDouble(),
      createdAt: DateTime.tryParse(json['createdAt'] as String? ?? '') ?? DateTime.now(),
    );
  }
}
