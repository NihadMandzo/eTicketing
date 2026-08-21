/// Central place for the Google Maps API key used by ProductLocationPicker's embedded webview
/// (the Windows-only pin picker/display — see product_location_picker.dart). Same
/// `--dart-define` convention as ApiConfig.baseUrl: override with
/// `--dart-define=GOOGLE_MAPS_API_KEY=...` for a real map; the placeholder below still renders
/// (Google serves a watermarked "for development purposes only" map without a valid key).
class MapsConfig {
  static const String googleMapsApiKey =
      String.fromEnvironment('GOOGLE_MAPS_API_KEY', defaultValue: 'YOUR_GOOGLE_MAPS_API_KEY');
}
