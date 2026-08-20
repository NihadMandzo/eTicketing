/// Mirrors the backend's `UpsertTicketTypeRequest` — same shape for create and update.
class TicketTypeUpsertRequest {
  final String name;
  final double price;

  const TicketTypeUpsertRequest({required this.name, required this.price});

  Map<String, dynamic> toJson() => {
        'Name': name,
        'Price': price,
      };
}
