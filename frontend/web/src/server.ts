import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import express from 'express';
import { join } from 'node:path';

const browserDistFolder = join(import.meta.dirname, '../browser');

const app = express();
const angularApp = new AngularNodeAppEngine();

/**
 * Example Express Rest API endpoints can be defined here.
 * Uncomment and define endpoints as necessary.
 *
 * Example:
 * ```ts
 * app.get('/api/{*splat}', (req, res) => {
 *   // Handle API request
 * });
 * ```
 */

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
