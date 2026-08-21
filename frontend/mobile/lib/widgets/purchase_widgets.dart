import 'package:flutter/material.dart';

import '../theme/app_colors.dart';

/// Small circular back-chevron button overlaid on a hero image — shared by
/// EventDetails/MuseumTicket/ParkingSpot (mockup screens 4/9/10).
class CircleBackButton extends StatelessWidget {
  final VoidCallback onTap;

  const CircleBackButton({super.key, required this.onTap});

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: InkWell(
        borderRadius: BorderRadius.circular(18),
        onTap: onTap,
        child: Container(
          width: 36,
          height: 36,
          margin: const EdgeInsets.all(12),
          decoration: BoxDecoration(color: Colors.black.withValues(alpha: 0.3), shape: BoxShape.circle),
          child: const Icon(Icons.arrow_back_rounded, color: Colors.white, size: 18),
        ),
      ),
    );
  }
}

/// One icon + text line — event date/time, location, organizer, etc.
class InfoRow extends StatelessWidget {
  final IconData icon;
  final String text;

  const InfoRow({super.key, required this.icon, required this.text});

  @override
  Widget build(BuildContext context) {
    final primary = Theme.of(context).colorScheme.primary;
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, size: 16, color: primary),
        const SizedBox(width: 10),
        Expanded(child: Text(text, style: const TextStyle(fontSize: 14))),
      ],
    );
  }
}

class StepperButton extends StatelessWidget {
  final IconData icon;
  final bool filled;
  final Color? borderColor;
  final Color? iconColor;
  final VoidCallback? onTap;

  const StepperButton({super.key, required this.icon, required this.filled, this.borderColor, this.iconColor, this.onTap});

  @override
  Widget build(BuildContext context) {
    final primary = Theme.of(context).colorScheme.primary;
    return InkWell(
      borderRadius: BorderRadius.circular(14),
      onTap: onTap,
      child: Container(
        width: 28,
        height: 28,
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          color: filled ? primary : Colors.transparent,
          border: filled ? null : Border.all(color: borderColor ?? primary, width: 2),
        ),
        child: Icon(icon, size: 14, color: filled ? Colors.white : iconColor),
      ),
    );
  }
}

/// A titled row with a price caption and a +/- quantity stepper — the
/// "Sektori"/"Vrsta ulaznice" line shared by EventDetails and MuseumTicket.
class QuantityRow extends StatelessWidget {
  final String title;
  final double price;
  final int quantity;
  final int maxQuantity;
  final VoidCallback onDecrement;
  final VoidCallback onIncrement;

  const QuantityRow({
    super.key,
    required this.title,
    required this.price,
    required this.quantity,
    this.maxQuantity = 10,
    required this.onDecrement,
    required this.onIncrement,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final primary = Theme.of(context).colorScheme.primary;
    final border = isDark ? AppColors.darkBorder : AppColors.lightBorder;

    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(title, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600)),
              Text('${price.toStringAsFixed(0)} KM', style: TextStyle(fontSize: 12, color: tertiaryText)),
            ],
          ),
        ),
        Row(
          children: [
            StepperButton(
              icon: Icons.remove_rounded,
              filled: false,
              borderColor: quantity > 0 ? primary : border,
              iconColor: quantity > 0 ? primary : (isDark ? AppColors.darkTextDisabled : AppColors.lightTextDisabled),
              onTap: quantity > 0 ? onDecrement : null,
            ),
            SizedBox(width: 24, child: Text('$quantity', textAlign: TextAlign.center, style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700))),
            StepperButton(icon: Icons.add_rounded, filled: quantity < maxQuantity, onTap: quantity < maxQuantity ? onIncrement : null),
          ],
        ),
      ],
    );
  }
}

/// Sticky bottom summary bar shared by EventDetails/MuseumTicket/ParkingSpot.
class PurchaseBottomBar extends StatelessWidget {
  final String totalLabel;
  final double total;
  final String buttonLabel;
  final bool enabled;
  final bool isLoading;
  final VoidCallback onPressed;

  const PurchaseBottomBar({
    super.key,
    required this.totalLabel,
    required this.total,
    required this.buttonLabel,
    required this.enabled,
    required this.isLoading,
    required this.onPressed,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final border = isDark ? AppColors.darkBorder : AppColors.lightBorder;
    final background = isDark ? AppColors.darkSurface : AppColors.lightSurface;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final primary = Theme.of(context).colorScheme.primary;

    return SafeArea(
      top: false,
      child: Container(
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 16),
        decoration: BoxDecoration(color: background, border: Border(top: BorderSide(color: border))),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(totalLabel, style: TextStyle(fontSize: 11, color: tertiaryText)),
                Text('${total.toStringAsFixed(0)} KM', style: TextStyle(fontSize: 19, fontWeight: FontWeight.w700, color: primary)),
              ],
            ),
            FilledButton(
              style: FilledButton.styleFrom(padding: const EdgeInsets.symmetric(horizontal: 28, vertical: 14)),
              onPressed: enabled ? onPressed : null,
              child: isLoading
                  ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                  : Text(buttonLabel),
            ),
          ],
        ),
      ),
    );
  }
}
