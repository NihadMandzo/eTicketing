import '../enums/ticketing_mode.dart';

/// Everything the export screen needs to draw itself: which sectors and price
/// tiers this product has, how many physical tickets each still has room for,
/// and where stub numbering will pick up.
///
/// Mirrors `TicketPrintOptionsResponse` in eTicketing.Ticketing.Business.
class TicketPrintOptionsResponse {
  final String productId;
  final String productName;
  final TicketingMode ticketingMode;
  final DateTime? productDate;

  /// False when this product cannot be exported at all — a monthly-reservation
  /// product, or one with no published sector yet. [blockedReason] then carries
  /// the Bosnian explanation to show in place of the export button.
  final bool canExport;
  final String? blockedReason;

  final int nextSerialNumber;
  final int maxTicketsPerBatch;

  /// How many tickets fit on one A4 sheet. Comes from the backend rather than
  /// being hard-coded here so the page-count preview can never disagree with
  /// what the renderer actually produces.
  final int ticketsPerSheet;

  final List<TicketPrintSectorOption> sectors;

  const TicketPrintOptionsResponse({
    required this.productId,
    required this.productName,
    required this.ticketingMode,
    required this.productDate,
    required this.canExport,
    required this.blockedReason,
    required this.nextSerialNumber,
    required this.maxTicketsPerBatch,
    required this.ticketsPerSheet,
    required this.sectors,
  });

  factory TicketPrintOptionsResponse.fromJson(Map<String, dynamic> json) {
    final productId = json['productId'] as String?;
    if (productId == null) throw const FormatException('TicketPrintOptionsResponse: productId je obavezan.');

    return TicketPrintOptionsResponse(
      productId: productId,
      productName: json['productName'] as String? ?? '',
      ticketingMode: TicketingMode.fromValue(json['ticketingMode'] as int? ?? 0),
      productDate: json['productDate'] == null ? null : DateTime.parse(json['productDate'] as String),
      canExport: json['canExport'] as bool? ?? false,
      blockedReason: json['blockedReason'] as String?,
      nextSerialNumber: json['nextSerialNumber'] as int? ?? 1,
      maxTicketsPerBatch: json['maxTicketsPerBatch'] as int? ?? 5000,
      ticketsPerSheet: json['ticketsPerSheet'] as int? ?? 3,
      sectors: (json['sectors'] as List<dynamic>? ?? const [])
          .map((e) => TicketPrintSectorOption.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }
}

class TicketPrintSectorOption {
  final String sectorId;
  final String name;
  final int capacity;

  /// How many admissions this sector still has free. The hard ceiling on how
  /// many tickets may be printed for it — the backend re-checks atomically, but
  /// the form must not let an organizer submit past it in the first place.
  final int remaining;

  final double price;
  final int? periodYear;
  final int? periodMonth;
  final List<TicketPrintTicketTypeOption> ticketTypes;

  const TicketPrintSectorOption({
    required this.sectorId,
    required this.name,
    required this.capacity,
    required this.remaining,
    required this.price,
    required this.periodYear,
    required this.periodMonth,
    required this.ticketTypes,
  });

  factory TicketPrintSectorOption.fromJson(Map<String, dynamic> json) {
    final sectorId = json['sectorId'] as String?;
    if (sectorId == null) throw const FormatException('TicketPrintSectorOption: sectorId je obavezan.');

    return TicketPrintSectorOption(
      sectorId: sectorId,
      name: json['name'] as String? ?? '',
      capacity: json['capacity'] as int? ?? 0,
      remaining: json['remaining'] as int? ?? 0,
      price: (json['price'] as num?)?.toDouble() ?? 0,
      periodYear: json['periodYear'] as int?,
      periodMonth: json['periodMonth'] as int?,
      ticketTypes: (json['ticketTypes'] as List<dynamic>? ?? const [])
          .map((e) => TicketPrintTicketTypeOption.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }
}

class TicketPrintTicketTypeOption {
  final String id;
  final String name;
  final double price;

  const TicketPrintTicketTypeOption({required this.id, required this.name, required this.price});

  factory TicketPrintTicketTypeOption.fromJson(Map<String, dynamic> json) {
    final id = json['id'] as String?;
    if (id == null) throw const FormatException('TicketPrintTicketTypeOption: id je obavezan.');

    return TicketPrintTicketTypeOption(
      id: id,
      name: json['name'] as String? ?? '',
      price: (json['price'] as num?)?.toDouble() ?? 0,
    );
  }
}
