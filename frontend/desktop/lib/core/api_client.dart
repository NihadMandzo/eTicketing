import 'package:cookie_jar/cookie_jar.dart';
import 'package:dio/dio.dart';
import 'package:dio_cookie_manager/dio_cookie_manager.dart';
import 'package:flutter/foundation.dart';
import 'package:path_provider/path_provider.dart';

import 'api_config.dart';

/// The single shared HTTP client for the whole app. Auth is httpOnly-cookie
/// based — the cookie jar attaches/receives `eticketing_at`/`eticketing_rt`
/// automatically on every request/response, so app code never touches a
/// token value directly (see [ApiConfig], [AuthProvider]).
///
/// The jar is file-backed (via [PersistCookieJar]) so a session survives
/// app restarts — call [initApiClient] once before `runApp`.
final Dio apiClient = Dio(BaseOptions(
  baseUrl: ApiConfig.baseUrl,
  contentType: 'application/json',
  connectTimeout: const Duration(seconds: 10),
  receiveTimeout: const Duration(seconds: 10),
  // Let callers inspect non-2xx responses themselves instead of throwing —
  // matches this app's existing ApiException/ApiError error-handling shape.
  // NOTE: this also means a 401 never reaches Dio's onError interceptor
  // hook (no exception is thrown for it) — the refresh-retry logic below
  // has to live in onResponse instead, not onError.
  validateStatus: (_) => true,
));

/// Set once by main.dart at startup, so this file (deliberately dependency-
/// free of any screen/widget) can still send the user back to the login
/// screen when a 401 survives a refresh attempt — i.e. the refresh cookie
/// itself is missing/expired/revoked, not just the access token.
VoidCallback? onSessionExpired;

/// The single in-flight `/Auth/refresh` call shared by every request that
/// hits a 401 while it's running. The refresh token rotates on every use
/// with reuse detection (see 01-domain.md's session/claims section) — if
/// two 401s each fired their own refresh call, the second would present an
/// already-rotated-out token and get the *whole* session revoked instead of
/// renewed, which is worse than the bug this interceptor fixes.
Future<bool>? _refreshFuture;

Future<void> initApiClient() async {
  final dir = await getApplicationSupportDirectory();
  final jar = PersistCookieJar(
    ignoreExpires: false,
    storage: FileStorage('${dir.path}/.cookies/'),
  );
  apiClient.interceptors.add(CookieManager(jar));
  apiClient.interceptors.add(InterceptorsWrapper(onResponse: _handleResponse));
}

/// On a 401 (expired access token), silently calls `/Auth/refresh` once —
/// concurrently-failing requests all await that same call rather than each
/// starting their own — and retries the original request. Never attempted
/// for the Auth endpoints themselves (a 401 from `Auth/login` means wrong
/// credentials, not an expired session, and refreshing off a 401 from
/// `Auth/refresh` itself would loop) or for a request that's already been
/// retried once (guards against looping if the retried call somehow 401s
/// again). Mirrors frontend/web's auth-refresh.interceptor.ts.
Future<void> _handleResponse(Response response, ResponseInterceptorHandler handler) async {
  final options = response.requestOptions;
  final isAuthEndpoint = options.path.contains('Auth/');
  final alreadyRetried = options.extra['isSessionRetry'] == true;

  if (response.statusCode != 401 || isAuthEndpoint || alreadyRetried) {
    return handler.next(response);
  }

  final refreshed = await (_refreshFuture ??= _refreshSession());
  if (!refreshed) {
    onSessionExpired?.call();
    return handler.next(response);
  }

  try {
    options.extra = {...options.extra, 'isSessionRetry': true};
    final retryResponse = await apiClient.fetch(options);
    return handler.resolve(retryResponse);
  } catch (_) {
    return handler.next(response);
  }
}

Future<bool> _refreshSession() async {
  try {
    final response = await apiClient.post('Auth/refresh');
    return response.statusCode != null && response.statusCode! >= 200 && response.statusCode! < 300;
  } catch (_) {
    return false;
  } finally {
    _refreshFuture = null;
  }
}
