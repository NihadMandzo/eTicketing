import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import express from 'express';
import { createProxyMiddleware } from 'http-proxy-middleware';
import { join } from 'node:path';

const browserDistFolder = join(import.meta.dirname, '../browser');

const app = express();
const angularApp = new AngularNodeAppEngine();

/**
 * Where this server reaches the Gateway. Read at runtime, from a name that deliberately does NOT
 * start with `NG_APP_`: `@ngx-env/builder` statically replaces every `NG_APP_*` reference with a
 * literal at build time — in the *server* bundle as well as the browser one, verified against the
 * committed build output — so a `NG_APP_`-prefixed name could never be changed per environment
 * without a rebuild. This one is an ordinary `process.env` lookup in a Node process, so it can.
 *
 * The default is the compose service name, which is what makes `docker compose up` work with no
 * configuration at all. In Azure Container Apps this is set to the Gateway's internal FQDN.
 */
const gatewayUrl = process.env['GATEWAY_INTERNAL_URL'] ?? 'http://gateway:8080';

/**
 * Proxy `/api/**` straight through to the Gateway, so the browser only ever talks to this origin.
 *
 * This is what makes `SameSite=Strict` on the session cookies possible (see
 * eTicketing.Shared.Auth.AuthCookiePolicy). Production previously ran the web app and the Gateway
 * as two separate Container Apps on unrelated hostnames — genuinely cross-site — which forced
 * `SameSite=None`, meaning the session rode along on cross-site requests to routes that
 * deliberately have no antiforgery token (the multipart uploads, called from Flutter desktop,
 * which cannot fetch one).
 *
 * Mounted **before** `express.static` so an `/api` path can never be answered by a stray file of
 * that name, and before the Angular handler so it is never treated as a route to render.
 *
 * `xfwd: true` appends this hop to `X-Forwarded-For`, which is how the real client IP survives as
 * far as Identity's rate limiter — without it every login would be throttled against this
 * container's address. It also lengthens the forwarded chain by one, which is what
 * `ForwardedHeaders:ForwardLimit` has to account for on the backend.
 *
 * The Gateway stays publicly published regardless: Flutter desktop, Flutter mobile, the IoT gate
 * firmware and Stripe's webhook all call it directly and never pass through here.
 */
app.use(
  '/api',
  createProxyMiddleware({
    target: gatewayUrl,
    changeOrigin: true,
    xfwd: true,
    // The mount path is stripped by Express before the middleware sees it, so it has to go back
    // on: the Gateway routes on `/api/**` (see gateway/appsettings.json) and would 404 without it.
    pathRewrite: (path) => `/api${path}`,
  }),
);

/**
 * Serve static files from /browser
 */
app.use(
  express.static(browserDistFolder, {
    maxAge: '1y',
    index: false,
    redirect: false,
  }),
);

/**
 * Handle all other requests by rendering the Angular application.
 *
 * `angularApp.handle(req)` resolving to `null` means nothing in the app matched this request at
 * all — not "the route matched and rendered a 404 page", but genuinely nothing. That happens for
 * every request whose path was never discovered by the build-time prerenderer: this app's
 * `outputMode: 'server'` only live-renders on demand for routes explicitly marked
 * `RenderMode.Server` (see app.routes.server.ts) — `dogadjaji/:id`, by design — everything else is
 * `RenderMode.Prerender`, which is a build-time-only mode with no request-time fallback (a plain
 * `'**'` route can't carry `getPrerenderParams`+`fallback`, the mechanism that would allow one; see
 * that file's comment). So a URL matching a real page renders straight from the static file
 * `express.static` already served above, and a URL matching nothing at all reaches here.
 *
 * The naive `next()` used to hand that case to Express's own bare-bones "Cannot GET /..." page —
 * this app's actual 404 splash (ErrorPageComponent) was built and wired into the client router's
 * `'**'` route, but never reached, because a genuinely bad URL never got that far. Serving the
 * prerendered `stranica-nije-pronadjena` page here — the same 404 content, parked at one concrete,
 * discoverable path purely so the prerenderer has something to generate — with a real `404` status
 * closes that gap without needing to enumerate every route this app has.
 */
app.use((req, res, next) => {
  angularApp
    .handle(req)
    .then((response) => {
      if (response) {
        writeResponseToNodeResponse(response, res);
        return;
      }
      res.status(404).sendFile(join(browserDistFolder, 'stranica-nije-pronadjena', 'index.html'), (err) => {
        if (err) next(err);
      });
    })
    .catch(next);
});

/**
 * Start the server if this module is the main entry point, or it is ran via PM2.
 * The server listens on the port defined by the `PORT` environment variable, or defaults to 4000.
 */
if (isMainModule(import.meta.url) || process.env['pm_id']) {
  const port = process.env['PORT'] || 4000;
  app.listen(port, (error) => {
    if (error) {
      throw error;
    }

    console.log(`Node Express server listening on http://localhost:${port}`);
  });
}

/**
 * Request handler used by the Angular CLI (for dev-server and during build) or Firebase Cloud Functions.
 */
export const reqHandler = createNodeRequestHandler(app);
