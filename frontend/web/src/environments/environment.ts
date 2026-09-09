export const environment = {
  production: true,
  apiBaseUrl: process.env['NG_APP_API_BASE_URL'] ?? 'http://localhost:5000/api',
  googleMapsApiKey: process.env['NG_APP_GOOGLE_MAPS_API_KEY'] ?? '',
};