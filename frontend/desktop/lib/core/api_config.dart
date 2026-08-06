/// Central place for the backend base URL.
///
/// Matches the Gateway's docker-compose default (port 5000 — see
/// `DESKTOP_API_BASE_URL` in `.env`/`docker-compose.yml`), overridable via
/// `--dart-define=API_BASE_URL=...` for local (non-docker) dev, e.g. against
/// the Gateway's `dotnet run` port: `--dart-define=API_BASE_URL=http://localhost:5262/api/`.
class ApiConfig {
  static const String baseUrl =
      String.fromEnvironment('API_BASE_URL', defaultValue: 'http://localhost:5000/api/');
}
