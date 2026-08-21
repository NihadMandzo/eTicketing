import '../enums/city.dart';

/// Same shape for create, update, and preview — mirrors the backend's UpsertProductRequest.
/// OrganizationId is never part of this request; the backend always takes it from the caller's
/// JWT claim. Images are managed exclusively through ProductProvider.uploadImage/deleteImage
/// (dedicated multipart endpoints), never bundled in here — same convention as Category's icon.
/// Latitude/Longitude/City are required — the organizer must place an exact pin, same tier as
/// name/category on the backend validator.
class ProductUpsertRequest {
  final String name;
  final String description;
  final DateTime? date;
  final int categoryId;
  final double? latitude;
  final double? longitude;
  final City? city;

  const ProductUpsertRequest({
    required this.name,
    this.description = '',
    this.date,
    required this.categoryId,
    this.latitude,
    this.longitude,
    this.city,
  });

  Map<String, dynamic> toJson() => {
        'Name': name,
        'Description': description,
        'Date': date?.toIso8601String(),
        'CategoryId': categoryId,
        'Latitude': latitude,
        'Longitude': longitude,
        'City': city?.value,
      };
}
