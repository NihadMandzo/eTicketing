/// One uploaded gallery photo — mirrors the backend's ProductImageResponse. Url is derived from
/// Azure Blob Storage on the backend, never a stored column; DisplayOrder reflects upload order
/// (first uploaded is the cover image).
class ProductImageResponse {
  final String id;
  final String url;
  final int displayOrder;

  const ProductImageResponse({
    required this.id,
    required this.url,
    required this.displayOrder,
  });

  factory ProductImageResponse.fromJson(Map<String, dynamic> json) => ProductImageResponse(
        id: json['id'] as String,
        url: json['url'] as String,
        displayOrder: json['displayOrder'] as int? ?? 0,
      );
}
