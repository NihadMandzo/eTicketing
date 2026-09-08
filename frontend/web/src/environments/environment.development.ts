// `ng serve` (dev server on :4200). Same `.env`-driven values as the production environment — only
// the `production` flag differs — so a local `.env` is all that separates the two. See
// environment.ts for how the values get here.
//
// The default `NG_APP_API_BASE_URL` points at the Gateway's real public port, 5000 (see
// .claude/rules/01-domain.md) — the same port docker-compose exposes, not the Gateway project's own
// plain `dotnet run` port (5262). Run the Gateway with `dotnet run` directly instead of via Docker
// and you'd set `NG_APP_API_BASE_URL=http://localhost:5262/api` in the repo-root `.env`.

export const environment = {
  production: false,
  apiBaseUrl: import.meta.env.NG_APP_API_BASE_URL ?? 'http://localhost:5000/api',
  googleMapsApiKey: import.meta.env.NG_APP_GOOGLE_MAPS_API_KEY ?? '',
};
