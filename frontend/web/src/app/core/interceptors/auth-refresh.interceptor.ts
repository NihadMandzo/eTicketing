import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';

import { AuthService } from '../services/auth.service';

/**
 * The auth endpoints where a 401 is the *answer*, not an expired session — so refreshing and
 * retrying would be meaningless at best and an infinite loop at worst.
 *
 * Deliberately an explicit list rather than the `url.includes('/auth/')` catch-all this used to be.
 * That catch-all also excluded `/auth/me`, which is the one call the app makes on every page load
 * to restore the session: a visitor whose short-lived `eticketing_at` cookie had expired (but whose
 * `eticketing_rt` was still perfectly good) got a 401 that nothing refreshed, so the app rendered
 * them as signed out. They only "logged in" later, when some *other* request — starting a purchase,
 * say — took the 401 path this interceptor did handle, refreshed the session and populated
 * `currentUser` as a side effect. Which is exactly the "it only signs me in after I try to buy
 * something" bug.
 */
const NO_REFRESH_PATHS = [
  '/auth/login',
  '/auth/register',
  '/auth/refresh',
  '/auth/forgot-password',
  '/auth/reset-password',
];

/**
 * On a 401 (expired access token), silently calls `/auth/refresh` once and
 * retries the original request. Skipped for the endpoints listed above, where
 * a 401 means wrong credentials rather than an expired session — and, for
 * `/auth/refresh` itself, would loop.
 */
export const authRefreshInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const isNoRefreshEndpoint = NO_REFRESH_PATHS.some((path) => req.url.includes(path));

  return next(req).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        !isNoRefreshEndpoint
      ) {
        return authService.refresh().pipe(
          switchMap(() => next(req)),
          catchError((refreshError) => {
            authService.currentUser.set(null);
            return throwError(() => refreshError);
          }),
        );
      }
      return throwError(() => error);
    }),
  );
};
