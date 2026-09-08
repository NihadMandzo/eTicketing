// Production build.
//
// Both values come from the repo-root `.env` — the same file docker-compose reads — via
// `@ngx-env/builder`, which statically replaces each `import.meta.env.NG_APP_*` reference with a
// literal at build time (see `ngxEnv` in angular.json and the ambient types in src/env.d.ts).
// Angular has no runtime environment on the client, so this bake step is unavoidable; what it buys
// is that nothing here is hardcoded any more.
//
// The `??` fallbacks are the local-dev defaults, so a fresh clone with no `.env` still builds and
// points at the Gateway's real public port.
//
// Nothing in this file may hold a secret: everything reachable from here ships in the client
// bundle. See https://angular.dev/tools/cli/environments.

export const environment = {
  production: true,
  // Absolute, not a same-origin relative path: the web app (docker-compose `web` service) and the
  // Gateway (`gateway` service) are published on separate host ports.
  apiBaseUrl: import.meta.env.NG_APP_API_BASE_URL ?? 'http://localhost:5000/api',
  // Empty means no key was configured — product-details skips loading the Maps script entirely and
  // shows a labelled placeholder instead of Google's grey "for development purposes only" tile. A
  // real key needs the Maps JavaScript API enabled and should be HTTP-referrer restricted to this
  // origin.
  googleMapsApiKey: import.meta.env.NG_APP_GOOGLE_MAPS_API_KEY ?? '',
};
