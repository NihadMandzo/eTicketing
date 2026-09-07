import 'package:flutter/material.dart';

import '../models/responses/organization_response.dart';
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
///
/// Renders in two states. On sale, it is the stepper. Sold out ([isSoldOut]), the stepper is
/// replaced by a "Rasprodano" tag, the title drops to the disabled tone and the price is struck
/// through — the row stays visible on purpose, because "the VIP sector exists and is gone" is
/// information a buyer wants, while a silently missing row just looks like the event has fewer
/// sectors than the poster said.
class QuantityRow extends StatelessWidget {
  final String title;
  final double price;
  final int quantity;

  /// Effective ceiling for this row. Callers pass the smaller of the per-order cap and what is
  /// actually left in the sector, so the stepper cannot build a selection the hold would reject.
  final int maxQuantity;

  /// Nothing left to sell. The row goes read-only.
  final bool isSoldOut;

  /// Optional scarcity line under the price ("Još 3 mjesta"). Only ever set from a known count —
  /// see SectorResponse.isLowStock.
  final String? note;

  final VoidCallback onDecrement;
  final VoidCallback onIncrement;

  const QuantityRow({
    super.key,
    required this.title,
    required this.price,
    required this.quantity,
    this.maxQuantity = 10,
    this.isSoldOut = false,
    this.note,
    required this.onDecrement,
    required this.onIncrement,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final disabledText = isDark ? AppColors.darkTextDisabled : AppColors.lightTextDisabled;
    final primary = Theme.of(context).colorScheme.primary;
    final border = isDark ? AppColors.darkBorder : AppColors.lightBorder;

    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                title,
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w600,
                  color: isSoldOut ? disabledText : null,
                ),
              ),
              Text(
                '${price.toStringAsFixed(0)} KM',
                style: TextStyle(
                  fontSize: 12,
                  color: isSoldOut ? disabledText : tertiaryText,
                  decoration: isSoldOut ? TextDecoration.lineThrough : null,
                  decorationColor: disabledText,
                ),
              ),
              if (note != null && !isSoldOut) ...[
                const SizedBox(height: 2),
                Text(note!, style: TextStyle(fontSize: 12, fontWeight: FontWeight.w700, color: primary)),
              ],
            ],
          ),
        ),
        if (isSoldOut)
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
            decoration: BoxDecoration(
              color: isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted,
              borderRadius: BorderRadius.circular(6),
              border: Border.all(color: border),
            ),
            child: Text(
              'Rasprodano',
              style: TextStyle(fontSize: 11, fontWeight: FontWeight.w700, color: disabledText),
            ),
          )
        else
          Row(
            children: [
              StepperButton(
                icon: Icons.remove_rounded,
                filled: false,
                borderColor: quantity > 0 ? primary : border,
                iconColor: quantity > 0 ? primary : disabledText,
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

    // No inset of its own — SystemBarInset in main.dart already ends the app above the Android
    // navigation bar, so this bar's padding is the padding a reader actually sees.
    return Container(
      padding: const EdgeInsets.fromLTRB(20, 14, 20, 14),
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
          SizedBox(
            height: 48,
            child: FilledButton(
              style: FilledButton.styleFrom(padding: const EdgeInsets.symmetric(horizontal: 26)),
              onPressed: enabled ? onPressed : null,
              child: isLoading
                  ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                  : Text(buttonLabel),
            ),
          ),
        ],
      ),
    );
  }
}

/// The organizer behind a product — who is running this event, and how to reach
/// them. Shared by EventDetails/MuseumTicket/ParkingSpot so the three detail
/// screens stay in step; the web's equivalent lives in
/// `product-details.component.html`.
///
/// Rendered only when the organization actually loaded. That request is
/// deliberately allowed to fail silently on all three screens (organizer detail
/// is a nice-to-have, never a reason to break a product page), so the caller
/// passes null and this simply does not appear.
class OrganizerCard extends StatelessWidget {
  final OrganizationResponse organization;

  const OrganizerCard({super.key, required this.organization});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final border = isDark ? AppColors.darkBorder : AppColors.lightBorder;
    final tertiary = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        border: Border.all(color: border),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              _Logo(organization: organization, border: border),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      organization.name,
                      style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700),
                    ),
                    if (organization.address.isNotEmpty) ...[
                      const SizedBox(height: 2),
                      Text(
                        organization.address,
                        style: TextStyle(fontSize: 12, color: tertiary),
                      ),
                    ],
                  ],
                ),
              ),
            ],
          ),
          if (organization.description.isNotEmpty) ...[
            const SizedBox(height: 12),
            Text(
              organization.description,
              style: TextStyle(fontSize: 13, height: 1.4, color: tertiary),
            ),
          ],
          if (organization.email.isNotEmpty) ...[
            const SizedBox(height: 12),
            InfoRow(icon: Icons.mail_outline_rounded, text: organization.email),
          ],
          if (organization.phoneNumber.isNotEmpty) ...[
            const SizedBox(height: 8),
            InfoRow(icon: Icons.phone_outlined, text: organization.phoneNumber),
          ],
          if (organization.website != null && organization.website!.isNotEmpty) ...[
            const SizedBox(height: 8),
            InfoRow(icon: Icons.language_rounded, text: organization.website!),
          ],
        ],
      ),
    );
  }
}

class _Logo extends StatelessWidget {
  final OrganizationResponse organization;
  final Color border;

  const _Logo({required this.organization, required this.border});

  @override
  Widget build(BuildContext context) {
    final primary = Theme.of(context).colorScheme.primary;
    final logoUrl = organization.logoUrl;

    return Container(
      width: 48,
      height: 48,
      decoration: BoxDecoration(
        border: Border.all(color: border),
        borderRadius: BorderRadius.circular(12),
      ),
      clipBehavior: Clip.antiAlias,
      child: logoUrl != null && logoUrl.isNotEmpty
          ? Image.network(
              logoUrl,
              fit: BoxFit.contain,
              // A broken logo URL must not paint an error box over the card —
              // fall back to the same initials a missing logo gets.
              errorBuilder: (_, _, _) => _Initials(name: organization.name, color: primary),
            )
          : _Initials(name: organization.name, color: primary),
    );
  }
}

/// Stand-in for a missing logo: the first letter of each of the first two
/// words, so "Sunset Events d.o.o." reads as "SE".
class _Initials extends StatelessWidget {
  final String name;
  final Color color;

  const _Initials({required this.name, required this.color});

  @override
  Widget build(BuildContext context) {
    final initials = name
        .split(RegExp(r'\s+'))
        .where((word) => word.isNotEmpty)
        .take(2)
        .map((word) => word[0].toUpperCase())
        .join();

    return Center(
      child: Text(
        initials,
        style: TextStyle(fontSize: 15, fontWeight: FontWeight.w700, color: color),
      ),
    );
  }
}
