/// The organizer behind a product, as shown in the "Organizator" card on
/// EventDetailsScreen/MuseumTicketScreen/ParkingSpotScreen. Mirrors
/// `OrganizationPublicResponse` on the backend and
/// `core/models/organization.models.ts` on the web — widen all three together.
///
/// `userCount` and `isActive` are deliberately left out: they are back-office
/// facts about the account, not something a customer browsing an event needs.
class OrganizationResponse {
  final String id;
  final String name;
  final String description;
  final String address;
  final String phoneNumber;
  final String email;
  final String? website;
  final String? logoUrl;

  const OrganizationResponse({
    required this.id,
    required this.name,
    this.description = '',
    this.address = '',
    this.phoneNumber = '',
    this.email = '',
    this.website,
    this.logoUrl,
  });

  factory OrganizationResponse.fromJson(Map<String, dynamic> json) {
    return OrganizationResponse(
      id: json['id'] as String,
      name: json['name'] as String,
      description: json['description'] as String? ?? '',
      address: json['address'] as String? ?? '',
      phoneNumber: json['phoneNumber'] as String? ?? '',
      email: json['email'] as String? ?? '',
      website: json['website'] as String?,
      logoUrl: json['logoUrl'] as String?,
    );
  }
}
