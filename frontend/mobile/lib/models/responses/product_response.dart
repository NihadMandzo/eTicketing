import '../api_enum.dart';
import '../ticketing_mode.dart';

class ProductImageResponse {
  final String id;
  final String url;
  final int displayOrder;

  const ProductImageResponse({required this.id, required this.url, required this.displayOrder});

  factory ProductImageResponse.fromJson(Map<String, dynamic> json) {
    return ProductImageResponse(
      id: json['id'] as String,
      url: json['url'] as String,
      displayOrder: json['displayOrder'] as int? ?? 0,
    );
  }
}

class ProductResponse {
  final String id;
  final String name;
  final String description;
  final DateTime? date;
  final int categoryId;
  final String categoryName;
  final TicketingMode ticketingMode;
  final String organizationId;
  final String status;
  final List<ProductImageResponse> images;
  final DateTime createdAt;

  const ProductResponse({
    required this.id,
    required this.name,
    required this.description,
    this.date,
    required this.categoryId,
    required this.categoryName,
    required this.ticketingMode,
    required this.organizationId,
    required this.status,
    required this.images,
    required this.createdAt,
  });

  factory ProductResponse.fromJson(Map<String, dynamic> json) {
    return ProductResponse(
      id: json['id'] as String,
      name: json['name'] as String,
      description: json['description'] as String? ?? '',
      date: json['date'] != null ? DateTime.tryParse(json['date'] as String) : null,
      categoryId: json['categoryId'] as int,
      categoryName: json['categoryName'] as String? ?? '',
      ticketingMode: ticketingModeFromJson(json['ticketingMode']),
      organizationId: json['organizationId'] as String,
      status: publishStatusFromJson(json['status']),
      images: (json['images'] as List? ?? [])
          .map((e) => ProductImageResponse.fromJson(e as Map<String, dynamic>))
          .toList(),
      createdAt: DateTime.tryParse(json['createdAt'] as String? ?? '') ?? DateTime.now(),
    );
  }
}
