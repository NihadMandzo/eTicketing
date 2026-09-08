import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  {
    // Product IDs are dynamic data, not known at build time — render on
    // demand per-request instead of prerendering a fixed param set.
    path: 'dogadjaji/:id',
    renderMode: RenderMode.Server
  },
  {
    // These three fetch live data unconditionally from their constructors
    // (HomeComponent: categories/recommendations, ProductsComponent:
    // categories/products, ProfileComponent: loadTicketsPage) — the same
    // reason dogadjaji/:id is excluded above: they can't be prerendered
    // against data that isn't known (or even reachable) at build time.
    // Prerendering them made the whole build depend on the live API being
    // reachable from the build container; when it wasn't, one route's
    // worker timed out and killed every other route's prerender along
    // with it.
    path: '',
    renderMode: RenderMode.Server
  },
  {
    path: 'dogadjaji',
    renderMode: RenderMode.Server
  },
  {
    path: 'profil',
    renderMode: RenderMode.Server
  },
  {
    // Sweeps up every other concrete, parameter-free page this app's Angular Router config
    // resolves to (/pomoc, /prijava, /profil, ...) and prerenders it at build time.
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
    renderMode: RenderMode.Prerender
  }
];
