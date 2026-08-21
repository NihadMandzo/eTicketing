import 'package:flutter/material.dart';

import '../theme/app_colors.dart';

/// Shared confirmation dialog for every PUT/PATCH/DELETE-triggering action in this app — one
/// widget, reused everywhere a mutating action needs a "are you sure?" step, rather than each
/// screen rolling its own inline `AlertDialog`. Own component, not a port of desktop's
/// `ConfirmDialog` — same purpose, styled to match this app's mockup card language (16px-radius,
/// light shadow) instead of desktop's.
class ConfirmDialog {
  ConfirmDialog._();

  static Future<bool?> show(
    BuildContext context, {
    required String title,
    required String message,
    String confirmLabel = 'Potvrdi',
    String cancelLabel = 'Odustani',
    bool destructive = true,
  }) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        backgroundColor: isDark ? AppColors.darkSurface : Colors.white,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        title: Text(title, style: const TextStyle(fontWeight: FontWeight.w700)),
        content: Text(message),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(false),
            child: Text(cancelLabel),
          ),
          FilledButton(
            style: FilledButton.styleFrom(
              backgroundColor: destructive ? AppColors.errorDark : Theme.of(ctx).colorScheme.primary,
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
            ),
            onPressed: () => Navigator.of(ctx).pop(true),
            child: Text(confirmLabel),
          ),
        ],
      ),
    );
  }
}
