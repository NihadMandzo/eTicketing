/// The answer to one scan (`POST /api/tickets/validate`).
///
/// Arrives as a **200** even when [isValid] is false: "already used", "wrong
/// event", "expired" are displayable answers about a ticket, not transport
/// failures, and the scanner has to paint a red card with [message] rather
/// than a generic error toast. Genuine failures — 403 (not an organizer) and
/// 409 (another device is validating this same ticket right now) — still come
/// through as an `ApiException`.
///
/// [message] is authored server-side in Bosnian and rendered verbatim, so the
/// reason a person is turned away at the door can never drift between the API
/// and this screen.
class TicketValidationResponse {
  final bool isValid;
  final String code;
  final String message;
  final String? ticketId;
  final String? sectorName;
  final String? ticketTypeName;
  final String? holderEmail;
  final DateTime? validatedAt;

  const TicketValidationResponse({
    required this.isValid,
    required this.code,
    required this.message,
    this.ticketId,
    this.sectorName,
    this.ticketTypeName,
    this.holderEmail,
    this.validatedAt,
  });

  factory TicketValidationResponse.fromJson(Map<String, dynamic> json) {
    return TicketValidationResponse(
      isValid: json['isValid'] as bool? ?? false,
      code: json['code'] as String? ?? '',
      message: json['message'] as String? ?? '',
      ticketId: json['ticketId'] as String?,
      sectorName: json['sectorName'] as String?,
      ticketTypeName: json['ticketTypeName'] as String?,
      holderEmail: json['holderEmail'] as String?,
      validatedAt: json['validatedAt'] != null
          ? DateTime.tryParse(json['validatedAt'] as String)
          : null,
    );
  }
}
