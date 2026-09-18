/**
 * Types for the build-time environment variables `@ngx-env/builder` injects.
 *
 * The builder (see `ngxEnv` in angular.json) statically replaces every `import.meta.env.NG_APP_*`
 * reference with a literal at build time, reading the repo-root `.env` — the same file
 * docker-compose reads — plus anything already on `process.env`, which is how the values arrive
 * inside the Docker build where that `.env` is not in the context. The package ships no typings of
 * its own, so this declaration is what makes the references type-check.
 *
 * Everything declared here is compiled into the client bundle and is therefore public. Never add a
 * real secret; see https://angular.dev/tools/cli/environments. A Google Maps browser key is fine —
 * it is designed to be public and is secured by HTTP-referrer restriction, not by being hidden.
 */
interface ImportMetaEnv {
  // NG_APP_API_BASE_URL used to live here. It is gone on purpose: the browser now calls a
  // same-origin, relative `/api` that this app's own server proxies to the Gateway (src/server.ts),
  // which is what lets the session cookies be SameSite=Strict. The server side of that proxy reads
  // GATEWAY_INTERNAL_URL — deliberately *not* an NG_APP_ name, because every NG_APP_ reference is
  // replaced with a build-time literal in the server bundle too, and that value has to be settable
  // per environment without a rebuild.

  /** Google Maps JavaScript API key for the product-details map. Empty/absent is a supported
   * state — the map is replaced by a labelled placeholder rather than Google's watermarked tile. */
  readonly NG_APP_GOOGLE_MAPS_API_KEY?: string;

  /** Set by the builder itself from `NG_APP_ENV`, defaulting to the build configuration name. */
  readonly NG_APP_ENV?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
