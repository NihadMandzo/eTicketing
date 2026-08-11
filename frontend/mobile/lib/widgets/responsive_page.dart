import 'package:flutter/material.dart';

/// Constrains content to a comfortable reading width and centers it when
/// the window is wider than a typical phone screen (e.g. this Flutter app
/// running in a resizable window) — mirrors the max-width, 16px-edge-padding
/// treatment the web app uses at its own mobile breakpoint
/// (`@media (max-width: 768px)` in `frontend/web/src/styles.css`) instead of
/// letting form fields/cards stretch edge-to-edge on a wide window.
class ResponsivePage extends StatelessWidget {
  final Widget child;
  final double maxWidth;
  final EdgeInsetsGeometry padding;

  const ResponsivePage({
    super.key,
    required this.child,
    this.maxWidth = 480,
    this.padding = const EdgeInsets.all(16),
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: padding,
      child: Align(
        alignment: Alignment.topCenter,
        child: ConstrainedBox(
          constraints: BoxConstraints(maxWidth: maxWidth),
          child: child,
        ),
      ),
    );
  }
}
