import 'package:desktop/models/requests/gate_device_upsert_request.dart';
import 'package:desktop/models/responses/gate_device_response.dart';
import 'package:desktop/models/search_objects/gate_device_search_object.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('GateDeviceUpsertRequest', () {
    test('a scoped device sends exactly the ticked sectors', () {
      final json = const GateDeviceUpsertRequest(
        productId: 'p1',
        name: 'Ulaz A',
        allSectors: false,
        sectorIds: ['s1', 's2'],
      ).toJson();

      expect(json['AllSectors'], isFalse);
      expect(json['SectorIds'], ['s1', 's2']);
      expect(json['IsActive'], isTrue);
    });

    test('an all-sectors device sends an empty sector list even if one was left selected', () {
      // The two are mutually exclusive server-side. Sending stale ids alongside AllSectors would
      // have them validated for product membership and could fail the save for no reason.
      final json = const GateDeviceUpsertRequest(
        productId: 'p1',
        name: 'Glavni ulaz',
        allSectors: true,
        sectorIds: ['s1'],
      ).toJson();

      expect(json['AllSectors'], isTrue);
      expect(json['SectorIds'], isEmpty);
    });

    test('field names match the backend UpsertGateDeviceRequest', () {
      final json = const GateDeviceUpsertRequest(
        productId: 'p1',
        name: 'Ulaz A',
        allSectors: false,
        sectorIds: ['s1'],
        isActive: false,
      ).toJson();

      expect(json.keys, containsAll(['ProductId', 'Name', 'AllSectors', 'SectorIds', 'IsActive']));
      expect(json['IsActive'], isFalse);
    });
  });

  group('GateDeviceResponse', () {
    Map<String, dynamic> payload({
      bool allSectors = false,
      List<Map<String, dynamic>>? sectors,
      String? lastSeenAt,
    }) =>
        {
          'id': 'd1',
          'productId': 'p1',
          'name': 'Ulaz A',
          'keyPrefix': 'etk_gate_9f3a',
          'allSectors': allSectors,
          'isActive': true,
          'lastSeenAt': lastSeenAt,
          'createdAt': '2026-08-27T10:00:00Z',
          'sectors': sectors ?? [],
        };

    test('parses a multi-sector scope', () {
      final device = GateDeviceResponse.fromJson(payload(sectors: [
        {'id': 's1', 'name': 'VIP'},
        {'id': 's2', 'name': 'Loža'},
      ]));

      expect(device.allSectors, isFalse);
      expect(device.sectors.map((s) => s.name), ['VIP', 'Loža']);
    });

    test('parses an all-sectors device with no sector rows', () {
      final device = GateDeviceResponse.fromJson(payload(allSectors: true));

      expect(device.allSectors, isTrue);
      expect(device.sectors, isEmpty);
    });

    test('a device that has never checked in has a null lastSeenAt', () {
      expect(GateDeviceResponse.fromJson(payload()).lastSeenAt, isNull);
    });

    test('parses lastSeenAt when present', () {
      final device = GateDeviceResponse.fromJson(payload(lastSeenAt: '2026-08-27T11:30:00Z'));

      expect(device.lastSeenAt, DateTime.parse('2026-08-27T11:30:00Z'));
    });

    test('never exposes a full api key — only the display prefix', () {
      final device = GateDeviceResponse.fromJson(payload());

      expect(device.keyPrefix, 'etk_gate_9f3a');
      // The plaintext lives only on GateDeviceCreatedResponse, and only once.
      expect(payload().containsKey('apiKey'), isFalse);
    });

    test('throws rather than silently building a device with no id', () {
      final broken = payload()..remove('id');

      expect(() => GateDeviceResponse.fromJson(broken), throwsFormatException);
    });
  });

  group('GateDeviceCreatedResponse', () {
    test('carries both the device and its one-time key', () {
      final created = GateDeviceCreatedResponse.fromJson({
        'device': {
          'id': 'd1',
          'productId': 'p1',
          'name': 'Ulaz A',
          'keyPrefix': 'etk_gate_9f3a',
          'allSectors': false,
          'isActive': true,
          'createdAt': '2026-08-27T10:00:00Z',
          'sectors': [
            {'id': 's1', 'name': 'VIP'}
          ],
        },
        'apiKey': 'etk_gate_9f3aQQQQQQQQQQQQQQQQQQQQQQ',
      });

      expect(created.device.name, 'Ulaz A');
      expect(created.apiKey, startsWith('etk_gate_'));
      expect(created.apiKey, startsWith(created.device.keyPrefix));
    });
  });

  group('GateDeviceSearchObject', () {
    test('omits the product filter when none is chosen', () {
      final query = GateDeviceSearchObject(page: 0, pageSize: 10).toQueryString();

      expect(query.containsKey('ProductId'), isFalse);
    });

    test('sends the product filter when chosen', () {
      final query = GateDeviceSearchObject(page: 1, pageSize: 10, productId: 'p1').toQueryString();

      expect(query['ProductId'], 'p1');
    });
  });
}
