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
  /** Absolute base URL of the Gateway, e.g. `http://localhost:5000/api`. Absolute, not
   * same-origin-relative: the web app and the Gateway are published on separate host ports. */
  readonly NG_APP_API_BASE_URL?: string;

  /** Google Maps JavaScript API key for the product-details map. Empty/absent is a supported
   * state — the map is replaced by a labelled placeholder rather than Google's watermarked tile. */
  readonly NG_APP_GOOGLE_MAPS_API_KEY?: string;

  /** Set by the builder itself from `NG_APP_ENV`, defaulting to the build configuration name. */
  readonly NG_APP_ENV?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
