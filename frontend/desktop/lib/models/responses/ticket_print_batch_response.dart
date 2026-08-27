/// Mirrors the backend's `TicketPrintBatchStatus` ordinals exactly. Raw-numeric
/// JSON convention, same as every other enum in this app.
enum TicketPrintBatchStatus {
  queued(0),
  rendering(1),
  ready(2),
  failed(3);

  final int value;
  const TicketPrintBatchStatus(this.value);

  static TicketPrintBatchStatus fromValue(int value) => TicketPrintBatchStatus.values.firstWhere(
        (s) => s.value == value,
        orElse: () => TicketPrintBatchStatus.queued,
      );

  /// True while the render worker still has work to do on this batch.
  bool get isInFlight => this == TicketPrintBatchStatus.queued || this == TicketPrintBatchStatus.rendering;

  String get label => switch (this) {
        TicketPrintBatchStatus.queued => 'U redu čekanja',
        TicketPrintBatchStatus.rendering => 'Priprema PDF-a',
        TicketPrintBatchStatus.ready => 'Spremno za preuzimanje',
        TicketPrintBatchStatus.failed => 'Neuspješno',
      };
}

/// One bulk export of physical tickets. Carries no file bytes — downloading is
/// a separate call, and it is the only thing that ever moves the PDF.
///
/// Mirrors `TicketPrintBatchResponse` in eTicketing.Ticketing.Business.
class TicketPrintBatchResponse {
  final String id;
  final String productId;
  final String productName;
  final TicketPrintBatchStatus status;
  final int ticketCount;

  /// Advances while the worker renders, so the button can show real progress
  /// instead of an open-ended spinner.
  final int renderedCount;

  final int pageCount;
  final int serialFrom;
  final int serialTo;
  final double nominalValue;
  final DateTime? validDate;
  final int? fileSizeBytes;
  final String? errorMessage;
  final DateTime createdAt;
  final DateTime? completedAt;
  final DateTime? downloadedAt;

  const TicketPrintBatchResponse({
    required this.id,
    required this.productId,
    required this.productName,
    required this.status,
    required this.ticketCount,
    required this.renderedCount,
    required this.pageCount,
    required this.serialFrom,
    required this.serialTo,
    required this.nominalValue,
    required this.validDate,
    required this.fileSizeBytes,
    required this.errorMessage,
    required this.createdAt,
    required this.completedAt,
    required this.downloadedAt,
  });

  /// True when there is a file waiting that has not been collected yet. The
  /// backend destroys the stored copy on download, so this flips false for good
  /// once the organizer saves it.
  bool get isDownloadable => status == TicketPrintBatchStatus.ready && downloadedAt == null;

  factory TicketPrintBatchResponse.fromJson(Map<String, dynamic> json) {
    final id = json['id'] as String?;
    if (id == null) throw const FormatException('TicketPrintBatchResponse: id je obavezan.');

    final createdAt = json['createdAt'] as String?;
    if (createdAt == null) throw const FormatException('TicketPrintBatchResponse: createdAt je obavezan.');

    return TicketPrintBatchResponse(
      id: id,
      productId: json['productId'] as String? ?? '',
      productName: json['productName'] as String? ?? '',
      status: TicketPrintBatchStatus.fromValue(json['status'] as int? ?? 0),
      ticketCount: json['ticketCount'] as int? ?? 0,
      renderedCount: json['renderedCount'] as int? ?? 0,
      pageCount: json['pageCount'] as int? ?? 0,
      serialFrom: json['serialFrom'] as int? ?? 0,
      serialTo: json['serialTo'] as int? ?? 0,
      nominalValue: (json['nominalValue'] as num?)?.toDouble() ?? 0,
      validDate: json['validDate'] == null ? null : DateTime.parse(json['validDate'] as String),
      fileSizeBytes: (json['fileSizeBytes'] as num?)?.toInt(),
      errorMessage: json['errorMessage'] as String?,
      createdAt: DateTime.parse(createdAt),
      completedAt: json['completedAt'] == null ? null : DateTime.parse(json['completedAt'] as String),
      downloadedAt: json['downloadedAt'] == null ? null : DateTime.parse(json['downloadedAt'] as String),
    );
  }
}
