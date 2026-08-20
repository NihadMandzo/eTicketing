import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

import { Category } from '../../core/models/catalog.models';
import { CategoryIconComponent } from '../category-icon/category-icon.component';

/**
 * Category filter chips shared by Landing and Events — categories are
 * DB-driven (SuperAdmin-configurable), not a fixed hardcoded set, so chip
 * colors are picked deterministically from a small fixed palette by index
 * rather than by matching specific category names (the design mockup
 * hardcodes a name→color map, which doesn't hold up against real,
 * organizer-defined categories). The badge itself now shows a real
 * category-revealing icon (app-category-icon) instead of the category's
 * first letter.
 */
@Component({
  selector: 'app-category-chips',
  standalone: true,
  imports: [CategoryIconComponent],
  templateUrl: './category-chips.component.html',
  styleUrl: './category-chips.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CategoryChipsComponent {
  readonly categories = input.required<Category[]>();
  readonly selectedCategoryId = input<number | null>(null);
  readonly categorySelected = output<number | null>();

  private readonly palette = ['#E11D48', '#7C3AED', '#F59E0B', '#0D9488', '#DB2777', '#2563EB', '#3F8F4F', '#1D5B3A'];

  colorFor(index: number): string {
    return this.palette[index % this.palette.length];
  }

  select(id: number | null): void {
    this.categorySelected.emit(id);
  }
}
