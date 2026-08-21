import 'package:flutter/material.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';

import '../models/city.dart';
import '../theme/app_colors.dart';

/// Read-only pin display for a product's location — one shared widget used by all 3 product
/// detail screens (event/museum/parking), since `Product` carries `latitude`/`longitude`/`city`
/// regardless of `TicketingMode`. `google_maps_flutter` is officially supported on Android/iOS
/// (unlike desktop, which has no implementation at all — see frontend/desktop's
/// ProductLocationPicker, which embeds the Maps JS API via a webview instead).
class ProductLocationMap extends StatelessWidget {
  final double latitude;
  final double longitude;
  final City city;

  const ProductLocationMap({super.key, required this.latitude, required this.longitude, required this.city});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final position = LatLng(latitude, longitude);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Icon(Icons.location_on_outlined, size: 16, color: tertiaryText),
            const SizedBox(width: 6),
            Text('Lokacija: ${cityLabel(city)}', style: TextStyle(fontSize: 13, color: tertiaryText)),
          ],
        ),
        const SizedBox(height: 10),
        ClipRRect(
          borderRadius: BorderRadius.circular(12),
          child: SizedBox(
            height: 180,
            child: GoogleMap(
              initialCameraPosition: CameraPosition(target: position, zoom: 14),
              markers: {Marker(markerId: const MarkerId('product-location'), position: position)},
              zoomControlsEnabled: false,
              myLocationButtonEnabled: false,
              liteModeEnabled: true,
            ),
          ),
        ),
      ],
    );
  }
}
