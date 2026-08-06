import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';

import { AuthService } from '../services/auth.service';

/**
 * On a 401 (expired access token), silently calls `/auth/refresh` once and
 * retries the original request. Never attempts this for the auth endpoints
 * themselves — a 401 from `/auth/login`/`/auth/register` means wrong
 * credentials, not an expired session, and refreshing from a 401 on
 * `/auth/refresh` itself would loop.
 */
export const authRefreshInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const isAuthEndpoint = req.url.includes('/auth/');

  return next(req).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        !isAuthEndpoint
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
