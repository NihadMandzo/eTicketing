import 'dart:convert';
import 'dart:math';
import 'dart:typed_data';

import 'package:desktop/core/overload_retry_interceptor.dart';
import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

/// Answers each request with the next queued (status, body, headers) and counts the calls.
class _QueuedAdapter implements HttpClientAdapter {
  _QueuedAdapter(this._responses);

  final List<(int, Map<String, dynamic>, Map<String, List<String>>)> _responses;
  int calls = 0;

  @override
  Future<ResponseBody> fetch(RequestOptions options, Stream<Uint8List>? requestStream, Future<void>? cancelFuture) async {
    final (status, body, headers) = _responses[min(calls, _responses.length - 1)];
    calls++;
    return ResponseBody.fromString(
      jsonEncode(body),
      status,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
        ...headers,
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

const _overloaded = (503, {'code': 'server.overloaded', 'message': 'Sistem je trenutno preopterećen. Pokušajte ponovo za nekoliko sekundi.'}, {'retry-after': ['2']});

void main() {
  late List<Duration> waits;

  (Dio, _QueuedAdapter) build(List<(int, Map<String, dynamic>, Map<String, List<String>>)> responses) {
    final dio = Dio(BaseOptions(baseUrl: 'http://test/', validateStatus: (_) => true));
    final adapter = _QueuedAdapter(responses);
    dio.httpClientAdapter = adapter;
    dio.interceptors.add(OverloadRetryInterceptor(dio, random: Random(1), wait: (d) async => waits.add(d)));
    return (dio, adapter);
  }

  setUp(() => waits = []);

  test('retries an overloaded refusal and returns the eventual success', () async {
    final (dio, adapter) = build([_overloaded, (200, {'holdId': 'h1'}, {})]);

    final response = await dio.post('sectors/1/hold', data: {'quantity': 1});

    expect(response.statusCode, 200);
    expect(response.data['holdId'], 'h1');
    expect(adapter.calls, 2);
    expect(waits.single.inMilliseconds, inInclusiveRange(2000, 2999));
  });

  test('gives up after three retries and hands back the Bosnian message', () async {
    final (dio, adapter) = build([_overloaded]);

    final response = await dio.get('products');

    expect(response.statusCode, 503);
    expect(response.data['message'], startsWith('Sistem je trenutno preopterećen'));
    expect(adapter.calls, 4);
    expect(waits, hasLength(3));
  });

  test('does not retry a 503 that is not the overload refusal', () async {
    final (dio, adapter) = build([(503, {'code': 'payment.unavailable', 'message': 'Plaćanje trenutno nije dostupno.'}, {})]);

    final response = await dio.post('purchases', data: {});

    expect(response.statusCode, 503);
    expect(adapter.calls, 1);
    expect(waits, isEmpty);
  });

  test('does not retry other statuses', () async {
    final (dio, adapter) = build([(409, {'code': 'sector.no_capacity', 'message': 'Nema dovoljno slobodnog kapaciteta.'}, {})]);

    final response = await dio.post('sectors/1/hold', data: {});

    expect(response.statusCode, 409);
    expect(adapter.calls, 1);
  });

  test('caps an excessive Retry-After at ten seconds', () async {
    final (dio, _) = build([
      (503, {'code': 'server.overloaded'}, {'retry-after': ['600']}),
      (200, {}, {}),
    ]);

    await dio.get('categories');

    expect(waits.single.inMilliseconds, inInclusiveRange(10000, 10999));
  });
}
