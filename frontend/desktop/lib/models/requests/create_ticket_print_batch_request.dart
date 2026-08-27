/// POST /api/ticket-print-batches — mints the physical tickets and queues the
/// sheet for rendering. Mirrors `CreateTicketPrintBatchRequest` on the backend.
class CreateTicketPrintBatchRequest {
  final String productId;

  /// Required for a DailyEntry product and rejected for any other: which
  /// calendar day every ticket in the batch admits entry for.
  final DateTime? validDate;

  final List<TicketPrintLineRequest> lines;

  const CreateTicketPrintBatchRequest({
    required this.productId,
    this.validDate,
    required this.lines,
  });

  Map<String, dynamic> toJson() => {
        'productId': productId,
        // DateOnly on the wire — a full timestamp would be re-interpreted
        // against the server's zone and could land on the wrong day.
        if (validDate != null)
          'validDate':
              '${validDate!.year.toString().padLeft(4, '0')}-${validDate!.month.toString().padLeft(2, '0')}-${validDate!.day.toString().padLeft(2, '0')}',
        'lines': lines.map((l) => l.toJson()).toList(),
      };
}

class TicketPrintLineRequest {
  final String sectorId;

  /// Null for a sector that prices through `Sector.Price` rather than named
  /// tiers — the backend rejects the mismatch either way.
  final String? ticketTypeId;

  final int quantity;

  const TicketPrintLineRequest({
    required this.sectorId,
    this.ticketTypeId,
    required this.quantity,
  });

  Map<String, dynamic> toJson() => {
        'sectorId': sectorId,
        'ticketTypeId': ticketTypeId,
        'quantity': quantity,
      };
}
