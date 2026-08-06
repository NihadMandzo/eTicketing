// Production build. The web app (docker-compose `web` service) and the
// Gateway (`gateway` service) are published on separate host ports — see
// `WEB_API_BASE_URL` in `.env`/`docker-compose.yml` — so this must be an
// absolute URL, not a same-origin relative path.
export const environment = {
  production: true,
  apiBaseUrl: 'http://localhost:5000/api',
};
