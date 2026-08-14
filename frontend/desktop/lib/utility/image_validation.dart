import 'dart:typed_data';

/// Client-side mirror of the backend's upload byte-size cap for category icons /
/// organization logos (CategoryIconValidation.MaxPngBytes /
/// OrganizationLogoValidation.MaxBytes) — the backend validator is still authoritative and
/// re-checks regardless (see 00-workflow-and-testing.md); this just gives instant feedback
/// instead of a round-trip to the API. Squareness is no longer checked here — it's guaranteed
/// by construction: every icon/logo goes through ImageCropDialog's forced 1:1 crop before its
/// bytes ever reach this check.
class ImageValidation {
  ImageValidation._();

  /// Returns a Bosnian error message if [bytes] exceeds [maxBytes], or `null` if it's within
  /// the cap.
  static String? validateMaxBytes(Uint8List bytes, {required int maxBytes, required String sizeErrorMessage}) {
    return bytes.length > maxBytes ? sizeErrorMessage : null;
  }
}
