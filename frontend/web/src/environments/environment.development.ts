export const environment = {
  production: false,
  // Relative in development too: `ng serve` proxies /api to the Gateway via proxy.conf.json, so
  // the dev server is same-origin exactly as the production Node server is. See environment.ts.
  apiBaseUrl: '/api',
  googleMapsApiKey: process.env['NG_APP_GOOGLE_MAPS_API_KEY'] ?? '',
};
