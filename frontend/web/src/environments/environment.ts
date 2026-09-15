export const environment = {
  production: true,
  // Same-origin and relative, not an absolute Gateway URL. The browser reaches the API through
  // this app's own /api proxy (see src/server.ts, and proxy.conf.json for `ng serve`), which is
  // what lets the session cookies be SameSite=Strict — see eTicketing.Shared.Auth.AuthCookiePolicy.
  //
  // Server-side rendering cannot use a relative URL, since a Node process has no document origin
  // to resolve it against; SsrApiBaseInterceptor (registered only in app.config.server.ts) turns it
  // back into an absolute internal address for those requests.
  apiBaseUrl: '/api',
  googleMapsApiKey: process.env['NG_APP_GOOGLE_MAPS_API_KEY'] ?? '',
};
