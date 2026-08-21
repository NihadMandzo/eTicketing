import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

import { Category } from '../../core/models/catalog.models';
import { CategoryIconComponent } from '../category-icon/category-icon.component';

/**
 * Square category cards for the landing page — image (or curated glyph) centered top, name at
 * bottom. Replaces the pill/chip treatment there; the "Sve kategorije" catch-all pill is
 * deliberately gone (HomeComponent auto-selects the first real category instead), so unlike
 * CategoryChipsComponent's old shape this always has a selection once categories load.
 */
@Component({
  selector: 'app-category-cards',
  imports: [CategoryIconComponent],
  templateUrl: './category-cards.component.html',
  styleUrl: './category-cards.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CategoryCardsComponent {
  readonly categories = input.required<Category[]>();
  readonly selectedCategoryId = input<number | null>(null);
  readonly categorySelected = output<number>();

  select(id: number): void {
    this.categorySelected.emit(id);
  }
}
