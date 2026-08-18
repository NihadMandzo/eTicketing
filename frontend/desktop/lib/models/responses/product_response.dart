import '../enums/ticketing_mode.dart';

class ProductResponse {
  final String id;
  final String name;
  final String description;
  final DateTime? date; // only meaningful when ticketingMode == singleOccurrence
  final int categoryId;
  final String categoryName;
  final TicketingMode ticketingMode;
  final String organizationId;
  final int status; // 0 = Draft, 1 = Published (PublishStatus, serialized as a plain int)
  final String? imageUrl;
  final DateTime createdAt;

  const ProductResponse({
    required this.id,
    required this.name,
    required this.description,
    required this.date,
    required this.categoryId,
    required this.categoryName,
    required this.ticketingMode,
    required this.organizationId,
    required this.status,
    this.imageUrl,
    required this.createdAt,
  });

  bool get isPublished => status == 1;

  factory ProductResponse.fromJson(Map<String, dynamic> json) {
    if (json['id'] == null) throw const FormatException('Missing id in payload');
    if (json['name'] == null) throw const FormatException('Missing name in payload');
    if (json['organizationId'] == null) throw const FormatException('Missing organizationId in payload');
    if (json['createdAt'] == null) throw const FormatException('Missing createdAt in payload');

    return ProductResponse(
      id: json['id'] as String,
      name: json['name'] as String,
      description: json['description'] as String? ?? '',
      date: json['date'] != null ? DateTime.parse(json['date'] as String) : null,
      categoryId: json['categoryId'] as int? ?? 0,
      categoryName: json['categoryName'] as String? ?? '',
      ticketingMode: TicketingMode.fromValue(json['ticketingMode'] as int? ?? 0),
      organizationId: json['organizationId'] as String,
      status: json['status'] as int? ?? 0,
      imageUrl: json['imageUrl'] as String?,
      createdAt: DateTime.parse(json['createdAt'] as String),
    );
  }
}
