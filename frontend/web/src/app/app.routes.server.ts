import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  {
    // Product IDs are dynamic data, not known at build time — render on
    // demand per-request instead of prerendering a fixed param set.
    path: 'dogadjaji/:id',
    renderMode: RenderMode.Server
  },
  {
    path: '**',
    renderMode: RenderMode.Prerender
  }
];
