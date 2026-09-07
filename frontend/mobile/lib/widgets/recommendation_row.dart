import 'package:flutter/material.dart';

import '../models/responses/product_response.dart';
import 'product_card.dart';

/// A titled horizontal strip of [ProductCard]s, used by every recommendation surface
/// ("Preporučeno za vas", "Slično ovome", "Popularno"). Purely presentational — it
/// takes products and a title and knows nothing about how either was chosen.
///
/// Renders nothing when the list is empty rather than an empty-state message: an absent row reads
/// as "this screen has no recommendations section", while an empty one reads as "the
/// recommendations are broken". The API is built not to return nothing, so an empty list here means
/// something is genuinely wrong and quietly omitting the section is the kinder failure.
class RecommendationRow extends StatelessWidget {
  final String title;
  final List<ProductResponse> products;
  final bool isLoading;
  final void Function(ProductResponse product) onProductTap;

  const RecommendationRow({
    super.key,
    required this.title,
    required this.products,
    required this.onProductTap,
    this.isLoading = false,
  });

  /// Cards are fixed-width here rather than filling the viewport as they do in the vertical list —
  /// a horizontal strip has to show the edge of the next card to read as scrollable.
  static const double _cardWidth = 240;

  /// Sized to the card's text block plus a hero image, not padded out beyond it. Every card in a
  /// horizontal list must share one height, and any excess used to land as dead space between
  /// "Pogledaj detalje" and the card's bottom border; [ProductCard.fillHeight] now routes it into
  /// the image instead, so this number is free to sit just above what the text actually needs.
  static const double _rowHeight = 232;

  @override
  Widget build(BuildContext context) {
    if (isLoading) {
      return Padding(
        padding: const EdgeInsets.symmetric(vertical: 16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _Title(title),
            const SizedBox(height: 12),
            const Center(child: Padding(padding: EdgeInsets.all(16), child: CircularProgressIndicator())),
          ],
        ),
      );
    }

    if (products.isEmpty) return const SizedBox.shrink();

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _Title(title),
          const SizedBox(height: 12),
          SizedBox(
            height: _rowHeight,
            child: ListView.separated(
              scrollDirection: Axis.horizontal,
              // The row is laid out edge-to-edge by its parent, so the padding lives here — that
              // way the first and last card clear the screen edges but the scroll area itself
              // still runs the full width.
              padding: const EdgeInsets.symmetric(horizontal: 4),
              itemCount: products.length,
              separatorBuilder: (_, _) => const SizedBox(width: 12),
              itemBuilder: (context, index) => SizedBox(
                width: _cardWidth,
                child: ProductCard(
                  product: products[index],
                  onTap: () => onProductTap(products[index]),
                  fillHeight: true,
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _Title extends StatelessWidget {
  final String text;

  const _Title(this.text);

  @override
  Widget build(BuildContext context) => Text(
        text,
        style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w800),
      );
}
