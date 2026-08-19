import '../enums/ticketing_mode.dart';

class CategoryUpdateRequest {
  final String name;
  final String description;
  final bool isActive;
  final int displayOrder;
  final TicketingMode ticketingMode;

  const CategoryUpdateRequest({
    required this.name,
    this.description = '',
    this.isActive = true,
    this.displayOrder = 0,
    this.ticketingMode = TicketingMode.singleOccurrence,
  });

  // Plain JSON body now — Icon moved to the dedicated PUT /categories/{id}/icon
  // endpoint, so this request is no longer sent as multipart/form-data and
  // values keep their real types (not stringified for form fields).
  Map<String, dynamic> toJson() => {
        'Name': name,
        'Description': description,
        'IsActive': isActive,
        'DisplayOrder': displayOrder,
        'TicketingMode': ticketingMode.value,
      };
}
