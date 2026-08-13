import 'dart:typed_data';
import 'dart:ui' as ui;

/// Client-side mirror of the backend's CategoryIconValidation /
/// OrganizationLogoValidation square-aspect-ratio + size-cap rules — the
/// backend validator is still authoritative and re-checks regardless (see
/// 00-workflow-and-testing.md); this just gives instant feedback on the
/// picked file instead of a round-trip to the API.
class ImageValidation {
  ImageValidation._();

  /// Returns a Bosnian error message if [bytes] fails the size cap or isn't
  /// a square image, or `null` if it passes both checks.
  static Future<String?> validateSquare(
    Uint8List bytes, {
    required int maxBytes,
    required String sizeErrorMessage,
    required String aspectRatioErrorMessage,
  }) async {
    if (bytes.length > maxBytes) return sizeErrorMessage;

    final codec = await ui.instantiateImageCodec(bytes);
    final frame = await codec.getNextFrame();
    final width = frame.image.width;
    final height = frame.image.height;
    frame.image.dispose();

    if (width != height) return aspectRatioErrorMessage;
    return null;
  }
}
