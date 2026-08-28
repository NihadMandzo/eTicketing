import { ChangeDetectionStrategy, Component, input } from '@angular/core';

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
}
