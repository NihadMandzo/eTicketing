/// Same shape for create, update, and preview — mirrors the backend's UpsertProductRequest.
/// OrganizationId is never part of this request; the backend always takes it from the caller's
/// JWT claim. Images are managed exclusively through ProductProvider.uploadImage/deleteImage
/// (dedicated multipart endpoints), never bundled in here — same convention as Category's icon.
class ProductUpsertRequest {
  final String name;
  final String description;
  final DateTime? date;
  final int categoryId;

  const ProductUpsertRequest({
    required this.name,
    this.description = '',
    this.date,
    required this.categoryId,
  });

  Map<String, dynamic> toJson() => {
        'Name': name,
        'Description': description,
        'Date': date?.toIso8601String(),
        'CategoryId': categoryId,
      };
}
