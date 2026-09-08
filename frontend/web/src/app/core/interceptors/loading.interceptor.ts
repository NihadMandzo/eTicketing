import { HttpContextToken, HttpInterceptorFn } from '@angular/common/http';
import { PLATFORM_ID, inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { finalize } from 'rxjs';

import { LoadingService } from '../services/loading.service';

/**
 * Opt a single request out of the global blocking overlay.
 *
 * Use it **only** for fire-and-forget calls the visitor did not ask for and whose result they never
 * see — freezing the page for one of those means the screen greys out a moment after it was already
 * usable, for no reason the visitor can connect to anything they did. Everything a person actually
 * triggered keeps the overlay.
 *
 * ```ts
 * this.http.post(url, body, { context: new HttpContext().set(SKIP_LOADING_OVERLAY, true) });
 * ```
 */
export const SKIP_LOADING_OVERLAY = new HttpContextToken<boolean>(() => false);

/**
 * Counts every backend request into {@link LoadingService}, which the global overlay reads.
 *
 * Browser-only. During SSR/prerender the component tree renders once, synchronously, after the
 * requests resolve — there is no interactive page to protect and no second paint in which an
 * overlay could appear, so counting there would only risk shipping a `pendingCount > 0` state into
 * the hydrated client.
 *
 * `finalize`, not `tap`: it runs on success, error **and** unsubscription. The last one matters —
 * a `switchMap` that abandons an in-flight request (or a component destroyed mid-load) cancels it
 * without ever emitting, and a `tap`-based counter would never come back down.
 */
export const loadingInterceptor: HttpInterceptorFn = (req, next) => {
  const isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  if (!isBrowser || req.context.get(SKIP_LOADING_OVERLAY)) {
    return next(req);
  }

  const loadingService = inject(LoadingService);
  loadingService.start();

  return next(req).pipe(finalize(() => loadingService.stop()));
};
