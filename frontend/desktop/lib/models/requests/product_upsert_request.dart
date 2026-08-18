/// Same shape for create, update, and preview — mirrors the backend's
/// UpsertProductRequest. OrganizationId is never part of this request; the backend always takes
/// it from the caller's JWT claim.
class ProductUpsertRequest {
  final String name;
  final String description;
  final DateTime? date;
  final int categoryId;
  final String? imageUrl;

  const ProductUpsertRequest({
    required this.name,
    this.description = '',
    this.date,
    required this.categoryId,
    this.imageUrl,
  });

  Map<String, dynamic> toJson() => {
        'Name': name,
        'Description': description,
        'Date': date?.toIso8601String(),
        'CategoryId': categoryId,
        'ImageUrl': imageUrl,
      };
}
