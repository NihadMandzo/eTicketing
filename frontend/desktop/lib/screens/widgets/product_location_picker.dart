import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_inappwebview/flutter_inappwebview.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../core/maps_config.dart';
import '../../theme/app_colors.dart';

/// Default map center when no pin has been placed yet — Sarajevo, matching the backend seed
/// data's coordinates (see ProductSeeder.cs).
const _defaultLatitude = 43.8563;
const _defaultLongitude = 18.4131;

/// Embeds the Google Maps JS API (same page used on `frontend/web`) for the product-location pin
/// picker (create/edit) and read-only display (product detail screen) — one widget, parameterized
/// by [editable], since google_maps_flutter has no desktop implementation at all.
///
/// **Windows-only**: flutter_inappwebview's only desktop backend is Windows
/// (flutter_inappwebview_windows) — there's no Linux implementation. On Linux this widget falls
/// back to plain latitude/longitude number fields instead of attempting to render a webview that
/// doesn't exist there.
class ProductLocationPicker extends StatefulWidget {
  final double? initialLatitude;
  final double? initialLongitude;
  final bool editable;

  /// Fired whenever the pin moves (a map click when [editable], or a manual field edit on the
  /// Linux fallback). Not called at all in read-only mode.
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
  late double _latitude = widget.initialLatitude ?? _defaultLatitude;
  late double _longitude = widget.initialLongitude ?? _defaultLongitude;

  bool get _isDark => Theme.of(context).brightness == Brightness.dark;

  /// flutter_inappwebview's only desktop backend is flutter_inappwebview_windows — there's no
  /// Linux implementation, so this is the one platform check that decides webview vs. fallback.
  bool get _supportsWebview => Platform.isWindows;

  @override
  Widget build(BuildContext context) {
    if (!_supportsWebview) return _buildFallback();

    return ClipRRect(
      borderRadius: BorderRadius.circular(12),
      child: SizedBox(
        height: 320,
        child: Stack(
          children: [
            InAppWebView(
              initialData: InAppWebViewInitialData(data: _buildHtml()),
              initialSettings: InAppWebViewSettings(javaScriptEnabled: true, transparentBackground: true),
              onWebViewCreated: (controller) {
                controller.addJavaScriptHandler(
                  handlerName: 'locationPicked',
                  callback: (args) {
                    if (!widget.editable) return;
                    final lat = (args[0] as num).toDouble();
                    final lng = (args[1] as num).toDouble();
                    setState(() {
                      _latitude = lat;
                      _longitude = lng;
                    });
                    widget.onLocationChanged?.call(lat, lng);
                  },
                );
              },
            ),
            if (widget.editable)
              Positioned(
                left: 8,
                bottom: 8,
                child: _CoordinatesBadge(latitude: _latitude, longitude: _longitude, isDark: _isDark),
              ),
          ],
        ),
      ),
    );
  }

  /// Manual lat/lng entry — the only option on Linux desktop builds (see class doc).
  Widget _buildFallback() {
    final borderColor = _isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput;
    final textPrimary = _isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary;
    final textTertiary = _isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(border: Border.all(color: borderColor), borderRadius: BorderRadius.circular(12)),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(LucideIcons.mapPin, size: 16, color: textTertiary),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  'Interaktivna mapa je dostupna samo na Windows verziji aplikacije. Unesite koordinate ručno.',
                  style: TextStyle(fontSize: 12, color: textTertiary),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(
                child: _CoordinateField(
                  label: 'Geografska širina',
                  value: _latitude,
                  isDark: _isDark,
                  onChanged: widget.editable
                      ? (v) {
                          setState(() => _latitude = v);
                          widget.onLocationChanged?.call(_latitude, _longitude);
                        }
                      : null,
                  textColor: textPrimary,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: _CoordinateField(
                  label: 'Geografska dužina',
                  value: _longitude,
                  isDark: _isDark,
                  onChanged: widget.editable
                      ? (v) {
                          setState(() => _longitude = v);
                          widget.onLocationChanged?.call(_latitude, _longitude);
                        }
                      : null,
                  textColor: textPrimary,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  String _buildHtml() {
    final clickHandler = widget.editable
        ? '''
      map.addListener('click', function(e) {
        var lat = e.latLng.lat();
        var lng = e.latLng.lng();
        marker.setPosition(e.latLng);
        window.flutter_inappwebview.callHandler('locationPicked', lat, lng);
      });
    '''
        : '';

    return '''
<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8" />
  <style>html, body, #map { height: 100%; margin: 0; padding: 0; }</style>
</head>
<body>
  <div id="map"></div>
  <script>
    function initMap() {
      var center = { lat: $_latitude, lng: $_longitude };
      var map = new google.maps.Map(document.getElementById('map'), {
        center: center,
        zoom: 14,
        disableDefaultUI: true,
        zoomControl: true,
        clickableIcons: false
      });
      var marker = new google.maps.Marker({ position: center, map: map${widget.editable ? '' : ', clickable: false'} });
      $clickHandler
    }
  </script>
  <script src="https://maps.googleapis.com/maps/api/js?key=${MapsConfig.googleMapsApiKey}&callback=initMap" async defer></script>
</body>
</html>
''';
  }
}

class _CoordinatesBadge extends StatelessWidget {
  final double latitude;
  final double longitude;
  final bool isDark;

  const _CoordinatesBadge({required this.latitude, required this.longitude, required this.isDark});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: (isDark ? AppColors.darkSurface : Colors.white).withValues(alpha: 0.9),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Text(
        '${latitude.toStringAsFixed(5)}, ${longitude.toStringAsFixed(5)}',
        style: TextStyle(
          fontSize: 11,
          fontFamily: 'monospace',
          color: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary,
        ),
      ),
    );
  }
}

class _CoordinateField extends StatelessWidget {
  final String label;
  final double value;
  final bool isDark;
  final ValueChanged<double>? onChanged;
  final Color textColor;

  const _CoordinateField({
    required this.label,
    required this.value,
    required this.isDark,
    required this.onChanged,
    required this.textColor,
  });

  @override
  Widget build(BuildContext context) {
    final borderColor = isDark ? AppColors.darkBorderInput : AppColors.lightBorderInput;
    return TextFormField(
      key: ValueKey('$label-${value.toStringAsFixed(5)}'),
      initialValue: value.toStringAsFixed(5),
      enabled: onChanged != null,
      keyboardType: const TextInputType.numberWithOptions(decimal: true, signed: true),
      style: TextStyle(fontSize: 13, color: textColor),
      decoration: InputDecoration(
        labelText: label,
        labelStyle: const TextStyle(fontSize: 12),
        isDense: true,
        contentPadding: const EdgeInsets.symmetric(horizontal: 10, vertical: 10),
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(10), borderSide: BorderSide(color: borderColor)),
      ),
      onChanged: (text) {
        final parsed = double.tryParse(text.trim());
        if (parsed != null) onChanged?.call(parsed);
      },
    );
  }
}
