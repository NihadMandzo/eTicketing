import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';

import { Product } from '../../core/models/catalog.models';

/**
 * Shared card for a Product in any browse grid (Landing's "Popularne
 * ulaznice", Events' SingleOccurrence grid). Product carries no price of its
 * own (Sectors do) — showing a per-card "Od X KM" price teaser would need an
 * extra request per product (Sectors aren't returned inline on the list
 * endpoint), so this card deliberately shows category/date/location only,
 * not a price teaser — price appears once the buyer opens product-details.
 */
@Component({
  selector: 'app-product-card',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './product-card.component.html',
  styleUrl: './product-card.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductCardComponent {
  readonly product = input.required<Product>();

  readonly coverImageUrl = computed(() => this.product().images[0]?.url ?? null);

  readonly formattedDate = computed(() => {
    const date = this.product().date;
    if (!date) return null;
    const d = new Date(date);
    const months = ['jan', 'feb', 'mar', 'apr', 'maj', 'jun', 'jul', 'aug', 'sep', 'okt', 'nov', 'dec'];
    return `${d.getDate()}. ${months[d.getMonth()]} ${d.getFullYear()}.`;
  });
}
