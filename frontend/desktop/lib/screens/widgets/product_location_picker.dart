import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';

import '../../theme/app_colors.dart';

/// Default map center when no pin has been placed yet — Sarajevo, matching the backend seed
/// data's coordinates (see ProductSeeder.cs).
const _defaultLatitude = 43.8563;
const _defaultLongitude = 18.4131;

/// Product-location pin picker (create/edit) and read-only display (product detail screen) —
/// one widget, parameterized by [editable]. Built on `flutter_map` + OpenStreetMap tiles: pure
/// Dart, no native plugin code, so no platform-specific build step at all — unlike
/// `google_maps_flutter` (no desktop implementation) or a native webview (every option considered
/// either targets an incompatible Dart SDK or fails to compile against this machine's MSVC/STL
/// toolchain). Web and mobile keep Google Maps; only desktop's map looks visually different (OSM
/// tiles), the stored `Latitude`/`Longitude` are identical either way.
class ProductLocationPicker extends StatefulWidget {
  final double? initialLatitude;
  final double? initialLongitude;
  final bool editable;

  /// Fired whenever the pin moves (a map tap when [editable]). Not called at all in read-only mode.
  final void Function(double latitude, double longitude)? onLocationChanged;

  const ProductLocationPicker({
    super.key,
    this.initialLatitude,
    this.initialLongitude,
    this.editable = true,
    this.onLocationChanged,
  });

  @override
  State<ProductLocationPicker> createState() => _ProductLocationPickerState();
}

class _ProductLocationPickerState extends State<ProductLocationPicker> {
  late LatLng _position = LatLng(
    widget.initialLatitude ?? _defaultLatitude,
    widget.initialLongitude ?? _defaultLongitude,
  );
  final _mapController = MapController();

  bool get _isDark => Theme.of(context).brightness == Brightness.dark;

  // Without this, editing a product's location and saving leaves this (read-only,
  // no-Key, same-tree-position) widget's State reused as-is — Flutter never re-runs the field
  // initializer above, so the map keeps showing the pre-edit pin until the screen fully remounts.
  @override
  void didUpdateWidget(covariant ProductLocationPicker oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.initialLatitude != oldWidget.initialLatitude ||
        widget.initialLongitude != oldWidget.initialLongitude) {
      final updated = LatLng(
        widget.initialLatitude ?? _defaultLatitude,
        widget.initialLongitude ?? _defaultLongitude,
      );
      setState(() => _position = updated);
      _mapController.move(updated, _mapController.camera.zoom);
    }
  }

  void _handleTap(TapPosition _, LatLng point) {
    if (!widget.editable) return;
    setState(() => _position = point);
    widget.onLocationChanged?.call(point.latitude, point.longitude);
  }

  @override
  Widget build(BuildContext context) {
    return ClipRRect(
      borderRadius: BorderRadius.circular(12),
      child: SizedBox(
        height: 320,
        child: Stack(
          children: [
            FlutterMap(
              mapController: _mapController,
              options: MapOptions(
                initialCenter: _position,
                initialZoom: 14,
                onTap: _handleTap,
                interactionOptions: InteractionOptions(
                  flags: widget.editable ? InteractiveFlag.all : InteractiveFlag.pinchZoom | InteractiveFlag.drag,
                ),
              ),
              children: [
                TileLayer(
                  urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                  userAgentPackageName: 'ba.eticketing.desktop',
                ),
                MarkerLayer(
                  markers: [
                    Marker(
                      point: _position,
                      width: 36,
                      height: 36,
                      child: Icon(Icons.location_pin, size: 36, color: AppColors.errorDark),
                    ),
                  ],
                ),
              ],
            ),
            Positioned(
              left: 8,
              bottom: 8,
              child: _CoordinatesBadge(position: _position, isDark: _isDark),
            ),
          ],
        ),
      ),
    );
  }
}

class _CoordinatesBadge extends StatelessWidget {
  final LatLng position;
  final bool isDark;

  const _CoordinatesBadge({required this.position, required this.isDark});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: (isDark ? AppColors.darkSurface : Colors.white).withValues(alpha: 0.9),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Text(
        '${position.latitude.toStringAsFixed(5)}, ${position.longitude.toStringAsFixed(5)}',
        style: TextStyle(
          fontSize: 11,
          fontFamily: 'monospace',
          color: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary,
        ),
      ),
    );
  }
}
