import 'package:flutter/material.dart';

import '../models/responses/product_response.dart';
import '../models/ticketing_mode.dart';
import '../theme/app_colors.dart';
import '../utils/category_icon.dart';

/// Shared card for a [ProductResponse] anywhere it appears in a list — the "Događaji" tab's
/// vertical list and the horizontal recommendation rows alike. Extracted from `events_screen.dart`
/// (where it was a private `_ProductCard`) when recommendations needed the same card, rather than
/// copied: two card widgets would be two places for the layout to drift.
///
/// Deliberately omits a price/location teaser — `Product` carries neither (only `Sector` has a
/// price, and there is no venue/location field), same reasoning as `ProductCardComponent` on
/// `frontend/web`.
class ProductCard extends StatelessWidget {
  final ProductResponse product;
  final VoidCallback onTap;

  /// Let the hero image absorb whatever height the card is given beyond what its text needs.
  ///
  /// Set by [RecommendationRow], whose horizontal list has to fix a row height for every card at
  /// once. Without this the text block kept its natural size and the leftover height piled up
  /// underneath "Pogledaj detalje" as dead space inside the card's border. Sending the slack to
  /// the image instead means the text sits tight against the bottom edge on every card, and the
  /// only thing that varies between them — a one- versus two-line title — changes the image height
  /// by a few pixels rather than opening a gap.
  ///
  /// Off in the vertical list, where the card's height is its content's and there is no slack.
  final bool fillHeight;

  const ProductCard({super.key, required this.product, required this.onTap, this.fillHeight = false});

  String get _subtitle => switch (product.ticketingMode) {
    TicketingMode.dailyEntry => 'Otvoreno svaki dan',
    TicketingMode.recurringReservation => 'Mjesečna pretplata',
    TicketingMode.singleOccurrence => product.date != null ? _formatDate(product.date!) : 'Datum nije naveden',
  };

  /// [Expanded] only makes sense inside the bounded-height row; in the vertical list the same
  /// wrapper would try to fill an unbounded column and throw.
  Widget _maybeExpanded(Widget child) => fillHeight ? Expanded(child: child) : child;

  static String _formatDate(DateTime date) {
    const months = [
      'Januar',
      'Februar',
      'Mart',
      'April',
      'Maj',
      'Juni',
      'Juli',
      'August',
      'Septembar',
      'Oktobar',
      'Novembar',
      'Decembar',
    ];
    return '${date.day} ${months[date.month - 1]} ${date.year}';
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final tertiaryText = isDark ? AppColors.darkTextTertiary : AppColors.lightTextTertiary;
    final primary = Theme.of(context).colorScheme.primary;

    return InkWell(
      borderRadius: BorderRadius.circular(16),
      onTap: onTap,
      child: Card(
        clipBehavior: Clip.antiAlias,
        margin: EdgeInsets.zero,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: fillHeight ? MainAxisSize.max : MainAxisSize.min,
          children: [
            _maybeExpanded(
              Stack(
                fit: StackFit.passthrough,
                children: [
                  Container(
                    height: fillHeight ? null : 130,
                    decoration: const BoxDecoration(
                      gradient: LinearGradient(
                        colors: [AppColors.primary, AppColors.secondary],
                        begin: Alignment.topLeft,
                        end: Alignment.bottomRight,
                      ),
                    ),
                    child: product.images.isNotEmpty
                        ? Image.network(
                            product.images.first.url,
                            fit: BoxFit.cover,
                            errorBuilder: (_, _, _) => const SizedBox.shrink(),
                          )
                        : null,
                  ),
                  Positioned(
                    top: 10,
                    right: 10,
                    child: Container(
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                      decoration: BoxDecoration(color: AppColors.accent, borderRadius: BorderRadius.circular(6)),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          CategoryIconGlyph(name: product.categoryName, size: 12, color: Colors.white),
                          const SizedBox(width: 5),
                          Text(
                            product.categoryName,
                            style: const TextStyle(color: Colors.white, fontSize: 11, fontWeight: FontWeight.w700),
                          ),
                        ],
                      ),
                    ),
                  ),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    _subtitle,
                    style: TextStyle(fontSize: 12, fontWeight: FontWeight.w700, color: primary),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    product.name,
                    style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700, height: 1.25),
                    // Two lines is the ceiling in both layouts: unbounded, a long title pushed the
                    // horizontal row's cards past their fixed height and clipped them.
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                  ),
                  const SizedBox(height: 10),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text('Pogledaj detalje', style: TextStyle(fontSize: 13, color: tertiaryText)),
                      Container(
                        width: 26,
                        height: 26,
                        decoration: BoxDecoration(
                          color: isDark ? AppColors.darkSurfaceMuted : AppColors.lightSurfaceMuted,
                          shape: BoxShape.circle,
                        ),
                        child: const Icon(Icons.chevron_right_rounded, size: 17),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
