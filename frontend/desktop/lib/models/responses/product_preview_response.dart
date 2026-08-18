import '../enums/ticketing_mode.dart';

/// Mirrors the backend's ProductPreviewResponse — returned by POST /products/preview, never
/// persisted. Shown to the organizer read-only before they confirm "Sačuvaj kao nacrt". Images
/// are deliberately absent: a previewed product has no Id yet to attach an uploaded image to
/// (same reasoning as Category, whose icon can only be uploaded once the category exists).
class ProductPreviewResponse {
  final String name;
  final String description;
  final DateTime? date;
  final int categoryId;
  final String categoryName;
  final TicketingMode ticketingMode;

  const ProductPreviewResponse({
    required this.name,
    required this.description,
    required this.date,
    required this.categoryId,
    required this.categoryName,
    required this.ticketingMode,
  });

  factory ProductPreviewResponse.fromJson(Map<String, dynamic> json) => ProductPreviewResponse(
        name: json['name'] as String? ?? '',
        description: json['description'] as String? ?? '',
        date: json['date'] != null ? DateTime.parse(json['date'] as String) : null,
        categoryId: json['categoryId'] as int? ?? 0,
        categoryName: json['categoryName'] as String? ?? '',
        ticketingMode: TicketingMode.fromValue(json['ticketingMode'] as int? ?? 0),
      );
}
