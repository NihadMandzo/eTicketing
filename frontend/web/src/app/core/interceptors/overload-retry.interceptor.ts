import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { PLATFORM_ID, inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { retry, throwError, timer } from 'rxjs';

/** The backend's load-shedding refusal — see LoadSheddingMiddleware. */
export const OVERLOADED_CODE = 'server.overloaded';

/** Attempts after the first. Three is enough to ride out a burst without keeping a visitor waiting
 * for long once the platform genuinely cannot serve them. */
export const OVERLOAD_MAX_RETRIES = 3;

/** Used when the response carries no usable Retry-After, and the most we will ever wait. */
const DEFAULT_RETRY_AFTER_MS = 2000;
const MAX_RETRY_AFTER_MS = 10_000;

/**
 * Retries a request the backend refused because it is overloaded (`503` with code
 * `server.overloaded`), after the `Retry-After` it asked for plus a little random jitter.
 *
 * **Only that one refusal.** The load-shedding middleware answers it before any work is done, so
 * repeating even a POST (a hold, a purchase) is safe. Every other 503 is left alone — in particular
 * `payment.unavailable` from the Payment circuit breaker, where the purchase was already processed
 * and its hold released, so a blind retry could only fail differently.
 *
 * Jitter, because every visitor refused in the same second would otherwise come back in the same
 * second and be refused together again.
 *
 * Browser-only: during SSR a delayed retry would only hold the render up. Once the retries run out
 * the error reaches the component unchanged, and `extractErrorMessage` shows the API's own Bosnian
 * text ("Sistem je trenutno preopterećen. Pokušajte ponovo za nekoliko sekundi.").
 *
 * Registered last in `app.config.ts`, so each retry re-sends only the HTTP call while the loading
 * overlay stays up for the whole wait.
 */
export const overloadRetryInterceptor: HttpInterceptorFn = (req, next) => {
  if (!isPlatformBrowser(inject(PLATFORM_ID))) {
    return next(req);
  }

  return next(req).pipe(
    retry({
      count: OVERLOAD_MAX_RETRIES,
      delay: (error: unknown) =>
        isOverloaded(error) ? timer(retryDelayMs(error) + Math.random() * 1000) : throwError(() => error),
    }),
  );
};

function isOverloaded(error: unknown): error is HttpErrorResponse {
  return (
    error instanceof HttpErrorResponse &&
    error.status === 503 &&
    (error.error as { code?: string } | null)?.code === OVERLOADED_CODE
  );
}

function retryDelayMs(error: HttpErrorResponse): number {
  const seconds = Number(error.headers?.get('Retry-After'));
  if (!Number.isFinite(seconds) || seconds <= 0) return DEFAULT_RETRY_AFTER_MS;
  return Math.min(seconds * 1000, MAX_RETRY_AFTER_MS);
}
