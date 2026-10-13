import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { Product } from '../../core/models/catalog.models';
import { ProductCardComponent } from '../product-card/product-card.component';

/**
 * A titled strip of product cards, used by every recommendation surface ("Preporučeno za vas",
 * "Slično ovome", "Popularno"). Purely presentational — it takes products and a
 * title, and knows nothing about how either was chosen.
 *
 * Renders nothing at all when the list is empty rather than an empty-state message: an absent row
 * reads as "this page has no recommendations section", while an empty one reads as "the
 * recommendations are broken". The API is built to avoid returning nothing, so an empty list here
 * means something is genuinely wrong and quietly omitting the section is the kinder failure.
 */
@Component({
  selector: 'app-recommendation-row',
  imports: [ProductCardComponent],
  templateUrl: './recommendation-row.component.html',
  styleUrl: './recommendation-row.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RecommendationRowComponent {
  readonly title = input.required<string>();
  readonly products = input.required<Product[]>();
  readonly isLoading = input(false);

  /** Optional line under the title, e.g. explaining why these products are being shown. */
  readonly description = input<string | null>(null);

  /** Renders the products as a single auto-scrolling strip instead of a wrapping grid. */
  readonly marquee = input(false);

  /**
   * The strip scrolls by translating a track holding two identical groups by -50%, so one group
   * has to be wider than the viewport or the loop shows a gap. A short list is repeated until it
   * clears {@link MIN_MARQUEE_ITEMS}; cards are 320px, so that covers a wide desktop screen.
   */
  protected readonly marqueeItems = computed(() => {
    const products = this.products();
    if (products.length === 0) return products;

    const repeats = Math.ceil(MIN_MARQUEE_ITEMS / products.length);
    return Array.from({ length: repeats }, () => products).flat();
  });

  /** Constant speed regardless of how many cards there are: ~7s per card. */
  protected readonly marqueeDuration = computed(() => `${this.marqueeItems().length * 7}s`);
}

const MIN_MARQUEE_ITEMS = 6;
