import { HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';

/**
 * Rewrites the app's relative `/api` calls to an absolute address during server-side rendering.
 *
 * `environment.apiBaseUrl` is relative so the browser stays same-origin with this app — that is
 * what allows `SameSite=Strict` session cookies. A Node process has no document origin, so the
 * same URL cannot be resolved there; Angular's `HttpClient` rejects a relative URL on the server
 * outright. The three per-request SSR routes (`''`, `dogadjaji`, `dogadjaji/:id` — see
 * app.routes.server.ts) all fetch on render, so without this they would each fail and the page
 * would arrive empty for the crawler and for the first paint.
 *
 * Registered **only** in `app.config.server.ts`, so the browser bundle never contains it. That is
 * deliberate rather than tidiness: `process.env` does not exist in a browser, and the name read
 * below is intentionally not `NG_APP_`-prefixed — so unlike the `NG_APP_*` variables, the builder
 * does not replace it with a literal and there would be nothing to fall back to.
 *
 * The internal address deliberately differs from what the browser uses: server-side rendering runs
 * inside the platform's own network and can reach the Gateway directly, without going back out
 * through this app's public origin and in again.
 */
@Injectable()
export class SsrApiBaseInterceptor implements HttpInterceptor {
  /**
   * Resolved once, at construction. The default matches docker-compose's service name so a local
   * `docker compose up` needs no configuration; Azure Container Apps sets it to the Gateway's
   * internal FQDN. Kept in step with the same default in `src/server.ts` — that one proxies the
   * browser's calls, this one makes the renderer's.
   */
  private readonly gatewayUrl = process.env['GATEWAY_INTERNAL_URL'] ?? 'http://gateway:8080';

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    if (!req.url.startsWith(environment.apiBaseUrl)) {
      return next.handle(req);
    }

    return next.handle(req.clone({ url: `${this.gatewayUrl}${req.url}` }));
  }
}
