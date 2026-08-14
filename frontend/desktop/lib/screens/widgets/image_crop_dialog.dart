import 'dart:typed_data';

import 'package:crop_your_image/crop_your_image.dart';
import 'package:flutter/material.dart';

import '../../theme/app_colors.dart';

/// Modal dialog that force-crops [imageBytes] to an exact 1:1 square before it's ever handed
/// off for upload. Category icons and organization logos are always rendered in a fixed square
/// slot (category chips, org avatars) — [ImageValidation] used to reject non-square uploads
/// after the fact; this guarantees squareness by construction instead, so there's nothing left
/// to reject. The backend's own square check (CategoryIconValidation/OrganizationLogoValidation)
/// stays authoritative regardless — see 00-workflow-and-testing.md.
class ImageCropDialog extends StatefulWidget {
  final Uint8List imageBytes;

  const ImageCropDialog({super.key, required this.imageBytes});

  /// Shows the dialog and returns the cropped PNG/JPEG bytes, or `null` if the user cancelled.
  static Future<Uint8List?> show(BuildContext context, Uint8List imageBytes) {
    return showDialog<Uint8List>(
      context: context,
      barrierDismissible: false,
      builder: (_) => ImageCropDialog(imageBytes: imageBytes),
    );
  }

  @override
  State<ImageCropDialog> createState() => _ImageCropDialogState();
}

class _ImageCropDialogState extends State<ImageCropDialog> {
  final _controller = CropController();
  bool _isCropping = false;

  bool get _isDark => Theme.of(context).brightness == Brightness.dark;

  void _confirm() {
    setState(() => _isCropping = true);
    _controller.crop();
  }

  void _onCropped(CropResult result) {
    switch (result) {
      case CropSuccess(:final croppedImage):
        if (mounted) Navigator.of(context).pop(croppedImage);
      case CropFailure(:final cause):
        if (mounted) {
          setState(() => _isCropping = false);
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text('Isijecanje slike nije uspjelo: $cause')),
          );
        }
    }
  }

  @override
  Widget build(BuildContext context) {
    final isDark = _isDark;
    final primary = isDark ? AppColors.secondary : AppColors.primary;
    final textPrimary = isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary;

    return Dialog(
      backgroundColor: Colors.transparent,
      insetPadding: const EdgeInsets.symmetric(horizontal: 32, vertical: 40),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 480, maxHeight: 560),
        child: ClipRRect(
          borderRadius: BorderRadius.circular(18),
          child: Material(
            color: isDark ? AppColors.darkSurface : Colors.white,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
                  color: isDark ? AppColors.darkSurfaceSubtle : AppColors.lightSurfaceSubtle,
                  child: Text(
                    'Isijecite sliku na kvadrat',
                    style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700, color: textPrimary),
                  ),
                ),
                SizedBox(
                  height: 360,
                  child: Crop(
                    image: widget.imageBytes,
                    controller: _controller,
                    aspectRatio: 1,
                    onCropped: _onCropped,
                    baseColor: isDark ? AppColors.darkBackground : Colors.black87,
                    maskColor: Colors.black.withValues(alpha: 0.6),
                  ),
                ),
                Padding(
                  padding: const EdgeInsets.all(16),
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.end,
                    children: [
                      OutlinedButton(
                        onPressed: _isCropping ? null : () => Navigator.of(context).pop(),
                        style: OutlinedButton.styleFrom(
                          foregroundColor: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary,
                          side: BorderSide(color: isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput),
                          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                          padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 10),
                        ),
                        child: const Text('Otkaži', style: TextStyle(fontWeight: FontWeight.w500, fontSize: 13)),
                      ),
                      const SizedBox(width: 10),
                      FilledButton(
                        onPressed: _isCropping ? null : _confirm,
                        style: FilledButton.styleFrom(
                          backgroundColor: primary,
                          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 10),
                          elevation: 0,
                        ),
                        child: _isCropping
                            ? const SizedBox(
                                width: 16,
                                height: 16,
                                child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                              )
                            : const Text(
                                'Isijeci i koristi',
                                style: TextStyle(fontWeight: FontWeight.w600, fontSize: 13, color: Colors.white),
                              ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
