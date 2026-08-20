import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { CatalogService } from '../../core/services/catalog.service';
import { Category, Product, TicketingMode } from '../../core/models/catalog.models';
import { CategoryChipsComponent } from '../../components/category-chips/category-chips.component';
import { ProductCardComponent } from '../../components/product-card/product-card.component';

@Component({
  selector: 'app-products',
  standalone: true,
  imports: [RouterLink, CategoryChipsComponent, ProductCardComponent],
  templateUrl: './products.component.html',
  styleUrl: './products.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductsComponent {
  private readonly catalogService = inject(CatalogService);

  readonly categories = signal<Category[]>([]);
  readonly products = signal<Product[]>([]);
  readonly selectedCategoryId = signal<number | null>(null);
  readonly searchText = signal('');
  readonly isLoading = signal(true);

  private searchDebounce?: ReturnType<typeof setTimeout>;

  /** Which layout to render — SingleOccurrence (or "sve kategorije") gets the
   * generic event-card grid; a specific DailyEntry/RecurringReservation
   * category switches to that mode's dedicated layout, matching the design. */
  readonly effectiveMode = computed<TicketingMode>(() => {
    const selectedId = this.selectedCategoryId();
    if (selectedId === null) return 'SingleOccurrence';
    return this.categories().find((c) => c.id === selectedId)?.ticketingMode ?? 'SingleOccurrence';
  });

  readonly resultCountLabel = computed(() => {
    const count = this.products().length;
    const mode = this.effectiveMode();
    if (mode === 'DailyEntry') return count === 1 ? '1 lokacija' : `${count} lokacije`;
    if (mode === 'RecurringReservation') return `${count} parking lokacije`;
    return `Pronađeno ${count} rezultata`;
  });

  constructor() {
    this.catalogService.getCategories().subscribe({
      next: (categories) => this.categories.set(categories),
      error: () => this.categories.set([]),
    });
    this.loadProducts();
  }

  onCategorySelected(categoryId: number | null): void {
    this.selectedCategoryId.set(categoryId);
    this.loadProducts();
  }

  onSearchChanged(value: string): void {
    this.searchText.set(value);
    if (this.searchDebounce) clearTimeout(this.searchDebounce);
    this.searchDebounce = setTimeout(() => this.loadProducts(), 300);
  }

  private loadProducts(): void {
    this.isLoading.set(true);
    this.catalogService
      .getProducts({
        page: 0,
        pageSize: 60,
        categoryId: this.selectedCategoryId(),
        fts: this.searchText().trim() || null,
      })
      .subscribe({
        next: (result) => {
          this.products.set(result.items);
          this.isLoading.set(false);
        },
        error: () => {
          this.products.set([]);
          this.isLoading.set(false);
        },
      });
  }
}
