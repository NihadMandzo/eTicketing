import 'package:cookie_jar/cookie_jar.dart';
import 'package:dio/dio.dart';
import 'package:dio_cookie_manager/dio_cookie_manager.dart';
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
  validateStatus: (_) => true,
));

Future<void> initApiClient() async {
  final dir = await getApplicationSupportDirectory();
  final jar = PersistCookieJar(
    ignoreExpires: false,
    storage: FileStorage('${dir.path}/.cookies/'),
  );
  apiClient.interceptors.add(CookieManager(jar));
}
