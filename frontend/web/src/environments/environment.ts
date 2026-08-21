// Production build. The web app (docker-compose `web` service) and the
// Gateway (`gateway` service) are published on separate host ports — see
// `WEB_API_BASE_URL` in `.env`/`docker-compose.yml` — so this must be an
// absolute URL, not a same-origin relative path.
export const environment = {
  production: true,
  apiBaseUrl: 'http://localhost:5000/api',
  // Compile-time constant, NOT read from the Dockerfile's GOOGLE_MAPS_API_KEY build arg — that
  // arg is reserved for future wiring (same "no-op today" status as WEB_API_BASE_URL above it in
  // the Dockerfile). Replace this placeholder with a real key before deploying; it needs the Maps
  // JavaScript API enabled and HTTP-referrer restricted to this origin. Until then the map renders
  // a grey "for development purposes only" tile.
  googleMapsApiKey: 'YOUR_GOOGLE_MAPS_API_KEY',
};
