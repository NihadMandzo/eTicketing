import { isPlatformBrowser } from '@angular/common';
import { PLATFORM_ID, inject } from '@angular/core';
import { CanActivateFn, Router, RouterStateSnapshot } from '@angular/router';

import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (_route, state: RouterStateSnapshot) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  // On the server this guard cannot answer the question it exists to answer, so it must not try.
  // `isAuthenticated()` reads `currentUser`, which is populated only by the browser-only
  // initializer in app.config.ts — the visitor's httpOnly cookie is never forwarded to SSR, and
  // during prerendering there is no visitor at all. A server-side check therefore doesn't report
  // "signed out", it reports "unknowable", and answering it anyway redirected signed-in users to
  // the login page: permanently, in the prerender case, because the redirect got baked into a
  // static file at build time.
  //
  // Returning `true` here defers the decision to the client, which re-runs this guard on
  // hydration with `currentUser` already resolved. Letting the shell render for a moment costs
  // nothing: this is a UX gate, not the security boundary — every private byte on these pages
  // comes from an API call the Gateway authorizes against the cookie independently.
  //
  // The guarded routes are also marked `RenderMode.Client` (app.routes.server.ts), which is the
  // real fix; this check is the backstop for the next guarded route that gets added without a
  // matching server-route entry, since the `'**'` fallback would silently prerender it.
  if (!isPlatformBrowser(inject(PLATFORM_ID))) {
    return true;
  }

  if (authService.isAuthenticated()) {
    return true;
  }

  // Carries where they were going, so signing in returns them there instead of the landing page.
  // `state.url` is the full attempted URL including its own query string, which is why it is passed
  // as a single encoded value rather than merged into these params.
  return router.createUrlTree(['/prijava'], { queryParams: { returnUrl: state.url } });
};
