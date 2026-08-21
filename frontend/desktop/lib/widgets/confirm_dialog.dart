import 'package:flutter/material.dart';

import '../theme/app_colors.dart';

/// Shared confirmation dialog for every PUT/PATCH/DELETE-triggering action in this app — one
/// widget, reused everywhere a mutating action needs a "are you sure?" step, rather than each
/// screen rolling its own inline `AlertDialog` (this used to be duplicated 5+ times, notably
/// ProductDetailScreen's private `_confirm` helper, now promoted here and used everywhere else
/// too). `confirmLabel`/`destructive` let callers use it for both destructive deletes (red button,
/// default) and non-destructive confirmations (e.g. "objavi", "spremi izmjene") with a neutral
/// button instead.
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
        title: Text(
          title,
          style: TextStyle(fontWeight: FontWeight.w700, color: isDark ? AppColors.darkTextPrimary : AppColors.lightTextPrimary),
        ),
        content: Text(
          message,
          style: TextStyle(color: isDark ? AppColors.darkTextSecondary : AppColors.lightTextSecondary),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(false),
            child: Text(cancelLabel, style: TextStyle(color: isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary)),
          ),
          ElevatedButton(
            style: ElevatedButton.styleFrom(
              backgroundColor: destructive ? AppColors.errorDark : (isDark ? AppColors.secondary : AppColors.primary),
              foregroundColor: Colors.white,
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
            ),
            onPressed: () => Navigator.of(ctx).pop(true),
            child: Text(confirmLabel),
          ),
        ],
      ),
    );
  }
}
