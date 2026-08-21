/// Only the fields this app actually reads (organizer name display on
/// EventDetailsScreen/MuseumTicketScreen/ParkingSpotScreen) — the backend
/// response carries more (address, phone, logo, ...).
class OrganizationResponse {
  final String id;
  final String name;

  const OrganizationResponse({required this.id, required this.name});

  factory OrganizationResponse.fromJson(Map<String, dynamic> json) {
    return OrganizationResponse(id: json['id'] as String, name: json['name'] as String);
  }
}
