import 'dart:math';

import 'package:dio/dio.dart';

/// Retries a request the backend refused because it is overloaded — HTTP 503
/// with `{ "code": "server.overloaded" }` from the load-shedding middleware —
/// after the `Retry-After` it asked for plus a little random jitter, at most
/// [maxRetries] times.
///
/// Only that one refusal: the middleware answers it before doing any work, so
/// repeating even a POST (a hold, a purchase) is safe. Any other 503 — e.g.
/// `payment.unavailable` from the Payment circuit breaker, after which the hold
/// is already released — is passed through untouched. Jitter, because every
/// client refused in the same second would otherwise return in the same second.
///
/// Lives in `onResponse`, not `onError`: this client uses
/// `validateStatus: (_) => true`, so a 503 arrives as a normal response. When
/// the retries run out the response goes on to the caller unchanged, and
/// `ApiError.fromJson` shows the API's own Bosnian message ("Sistem je trenutno
/// preopterećen. Pokušajte ponovo za nekoliko sekundi.").
class OverloadRetryInterceptor extends Interceptor {
  OverloadRetryInterceptor(
    this._dio, {
    this.maxRetries = 3,
    Random? random,
    Future<void> Function(Duration)? wait,
  })  : _random = random ?? Random(),
        _wait = wait ?? ((d) => Future<void>.delayed(d));

  final Dio _dio;
  final int maxRetries;
  final Random _random;

  /// How the interceptor sleeps between attempts — replaceable so a test can
  /// record the delays instead of waiting them out.
  final Future<void> Function(Duration) _wait;

  static const _attemptKey = 'overloadRetryAttempt';
  static const _defaultDelay = Duration(seconds: 2);
  static const _maxDelay = Duration(seconds: 10);

  @override
  Future<void> onResponse(Response response, ResponseInterceptorHandler handler) async {
    final options = response.requestOptions;
    final attempt = (options.extra[_attemptKey] as int?) ?? 0;

    if (!_isOverloaded(response) || attempt >= maxRetries) {
      return handler.next(response);
    }

    await _wait(_delayFor(response) + Duration(milliseconds: _random.nextInt(1000)));

    options.extra = {...options.extra, _attemptKey: attempt + 1};
    // A multipart body is a one-shot stream; it has to be cloned to be sent again.
    if (options.data is FormData) {
      options.data = (options.data as FormData).clone();
    }

    try {
      return handler.resolve(await _dio.fetch(options));
    } on DioException catch (e) {
      return handler.reject(e);
    }
  }

  static bool _isOverloaded(Response response) {
    final data = response.data;
    return response.statusCode == 503 && data is Map && data['code'] == 'server.overloaded';
  }

  static Duration _delayFor(Response response) {
    final seconds = int.tryParse(response.headers.value('retry-after') ?? '');
    if (seconds == null || seconds <= 0) return _defaultDelay;
    final asked = Duration(seconds: seconds);
    return asked > _maxDelay ? _maxDelay : asked;
  }
}
