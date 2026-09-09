export const environment = {
  production: true,
  apiBaseUrl: 'https://gateway.thankfulplant-ceb4a7f8.westeurope.azurecontainerapps.io/api',
  googleMapsApiKey: process.env['NG_APP_GOOGLE_MAPS_API_KEY'] ?? '',
};