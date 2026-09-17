import { HttpInterceptorFn } from '@angular/common/http';

/**
 * Attaches the httpOnly session cookie to every request.
 *
 * Kept even though the API is now same-origin — the app calls a relative `/api` that its own
 * server proxies to the Gateway — and `fetch` already sends cookies on same-origin requests by
 * default. This is explicit rather than implicit on purpose: the default is
 * `credentials: 'same-origin'`, so anything that ever made a request absolute again would stop
 * sending the session with no other symptom than being mysteriously signed out.
 */
export const credentialsInterceptor: HttpInterceptorFn = (req, next) =>
  next(req.clone({ withCredentials: true }));
