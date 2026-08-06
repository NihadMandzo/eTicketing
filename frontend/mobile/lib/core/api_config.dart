import 'dart:io' show Platform;

/// Central place for the backend base URL.
///
/// Matches the Gateway's docker-compose default (port 5000 — see
/// `MOBILE_API_BASE_URL` in `.env`/`docker-compose.yml`), baked in at build
/// time via `--dart-define=API_BASE_URL=...` (matches this app's Dockerfile
/// build arg already documented in the repo's `.env.example`).
///
/// When no override is supplied (plain `flutter run` during local dev), falls
/// back to a sensible per-platform default: the Android emulator can't reach
/// the host machine via `localhost` — it needs the special `10.0.2.2` alias —
/// while the iOS simulator (and desktop-style targets) can use `localhost`
/// directly. A real device needs `--dart-define` pointed at your LAN IP.
class ApiConfig {
  static const String _override = String.fromEnvironment('API_BASE_URL');

  static String get baseUrl {
    if (_override.isNotEmpty) return _override;
    if (!Platform.isAndroid) return 'http://localhost:5000/api/';
    return 'http://10.0.2.2:5000/api/';
  }
}
