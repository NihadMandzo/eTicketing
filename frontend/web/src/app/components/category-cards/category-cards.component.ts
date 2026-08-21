import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

import { Category } from '../../core/models/catalog.models';
import { CategoryIconComponent } from '../category-icon/category-icon.component';

/**
 * Square category cards for the landing page — image (or curated glyph) centered top, name at
 * bottom. Replaces the pill/chip treatment there; the "Sve kategorije" catch-all pill is
 * deliberately gone (HomeComponent auto-selects the first real category instead), so unlike
 * CategoryChipsComponent's old shape this always has a selection once categories load.
 * Colors are picked deterministically from a small fixed palette by index, same reasoning as the
 * old chips — categories are DB-driven, not a fixed hardcoded set.
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

  private readonly palette = ['#E11D48', '#7C3AED', '#F59E0B', '#0D9488', '#DB2777', '#2563EB', '#3F8F4F', '#1D5B3A'];

  colorFor(index: number): string {
    return this.palette[index % this.palette.length];
  }

  select(id: number): void {
    this.categorySelected.emit(id);
  }
}
