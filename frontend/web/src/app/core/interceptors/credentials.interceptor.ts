import { HttpInterceptorFn } from '@angular/common/http';

/**
 * Attaches the httpOnly session cookie to every request. Required because
 * the Angular dev server (:4200) and the Gateway (different port) are
 * cross-origin — without `withCredentials`, the browser won't send or
 * accept the `eticketing_at`/`eticketing_rt` cookies.
 */
export const credentialsInterceptor: HttpInterceptorFn = (req, next) =>
  next(req.clone({ withCredentials: true }));
