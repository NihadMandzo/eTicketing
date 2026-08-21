import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

import { Category } from '../../core/models/catalog.models';

/**
 * Category filter dropdown for the products listing page's 3-filter toolbar (search + category +
 * location). Replaces the old pill/chip row (CategoryChipsComponent, now removed) — that visual
 * style is reserved exclusively for the landing page's square category cards
 * (see CategoryCardsComponent). Unlike the cards, "Sve kategorije" is a real, selectable option
 * here — this page's default view genuinely means "every category", not "the first one".
 */
@Component({
  selector: 'app-category-select',
  templateUrl: './category-select.component.html',
  styleUrl: './category-select.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CategorySelectComponent {
  readonly categories = input.required<Category[]>();
  readonly selectedCategoryId = input<number | null>(null);
  readonly categorySelected = output<number | null>();

  onChange(value: string): void {
    this.categorySelected.emit(value === '' ? null : Number(value));
  }
}
