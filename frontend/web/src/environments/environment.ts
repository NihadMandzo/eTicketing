// Production build. The web app (docker-compose `web` service) and the
// Gateway (`gateway` service) are published on separate host ports — see
// `WEB_API_BASE_URL` in `.env`/`docker-compose.yml` — so this must be an
// absolute URL, not a same-origin relative path.
export const environment = {
  production: true,
  apiBaseUrl: 'http://localhost:5000/api',
  // Baked in at Docker build time via the GOOGLE_MAPS_API_KEY build arg (see Dockerfile) — the
  // placeholder below renders a grey "for development purposes only" map until a real key is
  // provisioned. Needs the Maps JavaScript API enabled, HTTP-referrer restricted to this origin.
  googleMapsApiKey: 'AIzaSyAE_ihJ2prQ0tJCNq2h0_LjPwDRk08VmxY',
};
