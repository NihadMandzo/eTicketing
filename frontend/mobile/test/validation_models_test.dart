import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/models/responses/ticket_response.dart';
import 'package:mobile/models/responses/ticket_validation_response.dart';
import 'package:mobile/models/responses/validation_product_response.dart';
import 'package:mobile/models/ticketing_mode.dart';

void main() {
  group('TicketResponse.fromJson', () {
    // Verbatim GET /api/tickets/mine item, ordinals and all.
    Map<String, dynamic> body() => {
      'id': '3f2a9c1e-0000-0000-0000-000000000001',
      'orderId': '3f2a9c1e-0000-0000-0000-000000000002',
      'sectorId': '3f2a9c1e-0000-0000-0000-000000000003',
      'sectorName': 'VIP',
      'productId': '3f2a9c1e-0000-0000-0000-000000000004',
      'ticketTypeId': null,
      'ticketTypeName': null,
      'status': 2,
      'pricePaid': 50,
      'validDate': null,
      'validFrom': null,
      'validTo': null,
      'createdAt': '2026-08-24T10:00:00Z',
      'qrPayload': 'ETK1.3f2a9c1e000000000000000000000001.AbCdEf',
      'qrImage': 'data:image/png;base64,iVBORw0KGgo=',
      'pdfUrl': 'https://storage.example/ticket-pdfs/order/ticket.pdf',
    };

    test('parses the QR and PDF fields', () {
      final ticket = TicketResponse.fromJson(body());

      expect(ticket.qrPayload, 'ETK1.3f2a9c1e000000000000000000000001.AbCdEf');
      expect(ticket.qrImage, startsWith('data:image/png;base64,'));
      expect(ticket.pdfUrl, isNotNull);
      expect(ticket.status, 'Ready');
    });

    test('tolerates a ticket whose PDF has not been generated yet', () {
      // Between purchase and PdfGeneration finishing, pdfUrl is genuinely null
      // — the app must render that state, not crash on it.
      final ticket = TicketResponse.fromJson(body()..['pdfUrl'] = null);

      expect(ticket.pdfUrl, isNull);
    });

    test('falls back to empty strings if the QR fields are absent', () {
      final json = body()
        ..remove('qrPayload')
        ..remove('qrImage');

      final ticket = TicketResponse.fromJson(json);

      expect(ticket.qrPayload, '');
      expect(ticket.qrImage, '');
    });
  });

  group('ValidationProductResponse.fromJson', () {
    test('parses counts and the ticketing-mode ordinal', () {
      final product = ValidationProductResponse.fromJson({
        'productId': '3f2a9c1e-0000-0000-0000-000000000004',
        'name': 'Ljetni Festival',
        'date': '2026-08-24T20:00:00Z',
        'ticketingMode': 1,
        'totalToday': 40,
        'validatedToday': 12,
      });

      expect(product.name, 'Ljetni Festival');
      expect(product.ticketingMode, TicketingMode.dailyEntry);
      expect(product.totalToday, 40);
      expect(product.validatedToday, 12);
      expect(product.remainingToday, 28);
    });

    test('tolerates a product with no date (DailyEntry / RecurringReservation)', () {
      final product = ValidationProductResponse.fromJson({
        'productId': '3f2a9c1e-0000-0000-0000-000000000004',
        'name': 'Muzej — dnevne ulaznice',
        'date': null,
        'ticketingMode': 1,
        'totalToday': 5,
        'validatedToday': 0,
      });

      expect(product.date, isNull);
    });
  });

  group('TicketValidationResponse.fromJson', () {
    test('parses a valid verdict', () {
      final result = TicketValidationResponse.fromJson({
        'isValid': true,
        'code': 'ticket.valid',
        'message': 'Ulaznica je validna. Ulaz odobren.',
        'ticketId': '3f2a9c1e-0000-0000-0000-000000000001',
        'sectorName': 'VIP',
        'ticketTypeName': 'Odrasli',
        'holderEmail': 'buyer@example.com',
        'validatedAt': '2026-08-24T10:05:00Z',
      });

      expect(result.isValid, isTrue);
      expect(result.message, 'Ulaznica je validna. Ulaz odobren.');
      expect(result.validatedAt, isNotNull);
    });

    test('parses an already-used verdict, keeping the first validation time', () {
      final result = TicketValidationResponse.fromJson({
        'isValid': false,
        'code': 'ticket.already_used',
        'message': 'Ulaznica je već iskorištena.',
        'ticketId': '3f2a9c1e-0000-0000-0000-000000000001',
        'sectorName': 'VIP',
        'ticketTypeName': null,
        'holderEmail': 'buyer@example.com',
        'validatedAt': '2026-08-24T09:30:00Z',
      });

      expect(result.isValid, isFalse);
      expect(result.code, 'ticket.already_used');
      expect(result.validatedAt, DateTime.utc(2026, 8, 24, 9, 30));
    });

    test('parses a forged-code verdict, which carries no ticket at all', () {
      final result = TicketValidationResponse.fromJson({
        'isValid': false,
        'code': 'ticket.qr_invalid',
        'message': 'Kod nije prepoznat. Ovo nije važeća eKarta ulaznica.',
        'ticketId': null,
        'sectorName': null,
        'ticketTypeName': null,
        'holderEmail': null,
        'validatedAt': null,
      });

      expect(result.isValid, isFalse);
      expect(result.ticketId, isNull);
      expect(result.validatedAt, isNull);
    });

    test('defaults to invalid if isValid is missing from the payload', () {
      // Fail closed: an unparseable verdict must never read as "let them in".
      final result = TicketValidationResponse.fromJson({'code': 'x', 'message': 'y'});

      expect(result.isValid, isFalse);
    });
  });
}
