import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  {
    // Product IDs are dynamic data, not known at build time — render on
    // demand per-request instead of prerendering a fixed param set.
    path: 'dogadjaji/:id',
    renderMode: RenderMode.Server,
  },
  {
    // These fetch live data unconditionally from their constructors
    // (HomeComponent: categories/recommendations, ProductsComponent:
    // categories/products) — the same
    // reason dogadjaji/:id is excluded above: they can't be prerendered
    // against data that isn't known (or even reachable) at build time.
    // Prerendering them made the whole build depend on the live API being
    // reachable from the build container; when it wasn't, one route's
    // worker timed out and killed every other route's prerender along
    // with it.
    path: '',
    renderMode: RenderMode.Server,
  },
  {
    path: 'dogadjaji',
    renderMode: RenderMode.Server,
  },
  {
    // The three routes behind `authGuard` (checkout, profile, e-mail confirmation).
    //
    // Client-rendered on purpose, and this is a correctness requirement rather than a
    // performance choice. `authGuard` decides from `AuthService.isAuthenticated()`, and auth
    // state is restored *only* in the browser — the initializer in app.config.ts bails out on
    // the server, because the visitor's httpOnly session cookie is never forwarded to SSR (and
    // at build time there is no visitor at all). So wherever these routes render on the server,
    // `currentUser` is unavoidably null, the guard unavoidably redirects, and the signed-in user
    // is bounced to /prijava.
    //
    // Under `RenderMode.Prerender` — which is what the `'**'` rule below silently applied to
    // `placanje` and `potvrda-emaila` — that redirect was evaluated once at build time and baked
    // into a static `<meta http-equiv="refresh">` file, so *every* visitor was sent to the login
    // page forever, signed in or not. Under `RenderMode.Server` (which `profil` used) it was the
    // same verdict recomputed as a 302 on every request. `RenderMode.Client` is the only mode
    // that defers the decision to the one place that can actually answer it: the browser, after
    // the initializer has resolved `/auth/me`.
    //
    // Nothing is lost by not rendering these on the server: all three are private pages whose
    // content comes from authenticated API calls that can only run client-side anyway, so the
    // server had nothing real to render.
    path: 'placanje',
    renderMode: RenderMode.Client,
  },
  {
    path: 'profil',
    renderMode: RenderMode.Client,
  },
  {
    path: 'potvrda-emaila',
    renderMode: RenderMode.Client,
  },
  {
    // Sweeps up every other concrete, parameter-free page this app's Angular Router config
    // resolves to (/pomoc, /prijava, /kontakt, ...) and prerenders it at build time.
    //
    // Deliberately NOT the whole story for `'**'` specifically: a bare Prerender route like this
    // has no request-time fallback (Angular's own types forbid `fallback` unless `getPrerenderParams`
    // is also supplied — see @angular/ssr's ServerRoutePrerender vs ServerRoutePrerenderWithParams),
    // and a Prerender match returns null at runtime regardless (only ever served from the static
    // file `express.static` already produced). So a URL that isn't one of the discoverable pages
    // above — including a genuinely bad one, which is exactly what the client router's own `'**'`
    // 404 route (app.routes.ts) exists to catch — has nothing to render here. `server.ts` covers
    // that gap explicitly, by serving the prerendered `stranica-nije-pronadjena` 404 page (itself
    // just another concrete page swept up by this same rule) whenever nothing else matched.
    path: '**',
    renderMode: RenderMode.Prerender,
  },
];
