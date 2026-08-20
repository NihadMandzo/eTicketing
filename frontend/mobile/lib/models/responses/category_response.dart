import '../ticketing_mode.dart';

class CategoryResponse {
  final int id;
  final String name;
  final TicketingMode ticketingMode;
  final String? iconUrl;

  const CategoryResponse({required this.id, required this.name, required this.ticketingMode, this.iconUrl});

  factory CategoryResponse.fromJson(Map<String, dynamic> json) {
    return CategoryResponse(
      id: json['id'] as int,
      name: json['name'] as String,
      ticketingMode: ticketingModeFromJson(json['ticketingMode']),
      iconUrl: json['iconUrl'] as String?,
    );
  }
}
