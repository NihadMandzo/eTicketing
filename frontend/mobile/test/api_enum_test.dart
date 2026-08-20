import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/models/api_enum.dart';
import 'package:mobile/models/responses/product_response.dart';
import 'package:mobile/models/responses/sector_response.dart';
import 'package:mobile/models/ticketing_mode.dart';

/// These cover the exact regression that made every product detail screen
/// report "Događaj nije pronađen": the API sends `"ticketingMode": 0` and
/// `"status": 1` as integers, and `json['x'] as String?` throws on an int
/// rather than falling back — a TypeError the screens' generic `catch`
/// swallowed into a not-found message.
void main() {
  group('enumNameFromJson', () {
    const names = ['Alpha', 'Beta', 'Gamma'];

    test('maps an integer ordinal onto the name at that position', () {
      expect(enumNameFromJson(0, names, 'Alpha'), 'Alpha');
      expect(enumNameFromJson(1, names, 'Alpha'), 'Beta');
      expect(enumNameFromJson(2, names, 'Alpha'), 'Gamma');
    });

    test('maps a numeric string ordinal the same way', () {
      expect(enumNameFromJson('1', names, 'Alpha'), 'Beta');
    });

    test('passes a known name straight through', () {
      expect(enumNameFromJson('Gamma', names, 'Alpha'), 'Gamma');
    });

    test('falls back for an out-of-range ordinal', () {
      expect(enumNameFromJson(9, names, 'Alpha'), 'Alpha');
      expect(enumNameFromJson(-1, names, 'Alpha'), 'Alpha');
    });

    test('falls back for an unknown name and for null', () {
      expect(enumNameFromJson('Delta', names, 'Alpha'), 'Alpha');
      expect(enumNameFromJson(null, names, 'Alpha'), 'Alpha');
    });
  });

  group('ticketingModeFromJson', () {
    test('matches the backend TicketingMode ordinals', () {
      expect(ticketingModeFromJson(0), TicketingMode.singleOccurrence);
      expect(ticketingModeFromJson(1), TicketingMode.dailyEntry);
      expect(ticketingModeFromJson(2), TicketingMode.recurringReservation);
    });

    test('accepts names too, in case the backend adds JsonStringEnumConverter', () {
      expect(ticketingModeFromJson('RecurringReservation'), TicketingMode.recurringReservation);
    });

    test('falls back to singleOccurrence on anything unrecognised', () {
      expect(ticketingModeFromJson(99), TicketingMode.singleOccurrence);
      expect(ticketingModeFromJson(null), TicketingMode.singleOccurrence);
    });
  });

  group('publishStatusFromJson / ticketStatusFromJson', () {
    test('match the backend PublishStatus and TicketStatus ordinals', () {
      expect(publishStatusFromJson(0), 'Draft');
      expect(publishStatusFromJson(1), 'Published');
      expect(ticketStatusFromJson(0), 'Processing');
      expect(ticketStatusFromJson(1), 'Confirmed');
      expect(ticketStatusFromJson(2), 'Ready');
      expect(ticketStatusFromJson(3), 'Cancelled');
    });
  });

  group('ProductResponse.fromJson', () {
    // Verbatim GET /api/products/{id} body, ordinals and all.
    final json = <String, dynamic>{
      'id': 'c3c3c3c3-0000-0000-0000-000000000007',
      'name': 'Jazz Noć na Baščaršiji',
      'description': 'Večer jazza uživo u srcu Sarajeva.',
      'date': '2026-09-12T20:00:00',
      'categoryId': 1,
      'categoryName': 'Muzika123',
      'ticketingMode': 0,
      'organizationId': 'a1a1a1a1-0000-0000-0000-000000000001',
      'status': 1,
      'images': <dynamic>[],
      'createdAt': '2026-08-20T00:00:00',
    };

    test('parses integer ticketingMode and status without throwing', () {
      final product = ProductResponse.fromJson(json);

      expect(product.ticketingMode, TicketingMode.singleOccurrence);
      expect(product.status, 'Published');
      expect(product.name, 'Jazz Noć na Baščaršiji');
      expect(product.date, DateTime(2026, 9, 12, 20));
    });

    test('tolerates a missing date (DailyEntry/RecurringReservation products)', () {
      final product = ProductResponse.fromJson({...json, 'date': null, 'ticketingMode': 1});

      expect(product.date, isNull);
      expect(product.ticketingMode, TicketingMode.dailyEntry);
    });
  });

  group('SectorResponse.fromJson', () {
    // Verbatim GET /api/sectors?productId= item, ticket types included.
    final json = <String, dynamic>{
      'id': 'd4d4d4d4-0000-0000-0000-000000000001',
      'productId': 'c3c3c3c3-0000-0000-0000-000000000007',
      'name': 'Standard',
      'capacity': 150,
      'price': 25.00,
      'status': 1,
      'ticketingMode': 0,
      'periodYear': null,
      'periodMonth': null,
      'createdAt': '2026-08-20T00:00:00',
      'ticketTypes': <dynamic>[
        {
          'id': 'e5e5e5e5-0000-0000-0000-000000000001',
          'sectorId': 'd4d4d4d4-0000-0000-0000-000000000001',
          'name': 'Odrasli',
          'price': 25.00,
          'createdAt': '2026-08-20T00:00:00',
        },
        {
          'id': 'e5e5e5e5-0000-0000-0000-000000000002',
          'sectorId': 'd4d4d4d4-0000-0000-0000-000000000001',
          'name': 'Studenti',
          'price': 18.00,
          'createdAt': '2026-08-20T00:00:00',
        },
      ],
    };

    test('parses integer enums and nested ticket types', () {
      final sector = SectorResponse.fromJson(json);

      expect(sector.ticketingMode, TicketingMode.singleOccurrence);
      expect(sector.status, 'Published');
      expect(sector.capacity, 150);
      expect(sector.ticketTypes, hasLength(2));
      expect(sector.ticketTypes.map((t) => t.name), ['Odrasli', 'Studenti']);
      expect(sector.ticketTypes.last.price, 18.0);
    });

    test('parses a sector with no ticket types (single implicit price)', () {
      final sector = SectorResponse.fromJson({...json, 'name': 'VIP', 'price': 45.00, 'ticketTypes': <dynamic>[]});

      expect(sector.ticketTypes, isEmpty);
      expect(sector.price, 45.0);
    });
  });
}
