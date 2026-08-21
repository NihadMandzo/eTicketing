import { isPlatformBrowser } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnDestroy, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { CatalogService } from '../../core/services/catalog.service';
import { Category, Product, TicketingMode } from '../../core/models/catalog.models';
import { CategoryChipsComponent } from '../../components/category-chips/category-chips.component';
import { ProductCardComponent } from '../../components/product-card/product-card.component';
import { PaginationBarComponent } from '../../components/pagination-bar/pagination-bar.component';

@Component({
  selector: 'app-products',
  standalone: true,
  imports: [RouterLink, CategoryChipsComponent, ProductCardComponent, PaginationBarComponent],
  templateUrl: './products.component.html',
  styleUrl: './products.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductsComponent implements OnDestroy {
  private readonly catalogService = inject(CatalogService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly categories = signal<Category[]>([]);
  readonly products = signal<Product[]>([]);
  readonly selectedCategoryId = signal<number | null>(null);
  readonly searchText = signal('');
  readonly isLoading = signal(true);

  // Pagination — the full catalog page always paginates (unlike the
  // landing page's fixed-size "Popularne ulaznice" preview, which
  // deliberately has no pager). 0-indexed, per the locked PagedResult
  // contract (see 01-domain.md).
  readonly page = signal(0);
  readonly pageSize = signal(20);
  readonly totalCount = signal(0);
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize())));

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
    const count = this.totalCount();
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
    this.page.set(0);
    this.loadProducts();
  }

  onSearchChanged(value: string): void {
    this.searchText.set(value);
    this.page.set(0);
    if (this.searchDebounce) clearTimeout(this.searchDebounce);
    this.searchDebounce = setTimeout(() => this.loadProducts(), 300);
  }

  onPageChanged(page: number): void {
    this.page.set(page);
    this.loadProducts();
    // Jumping pages without scrolling back up reads as "nothing happened"
    // on a long results grid. Guarded — `window` doesn't exist during SSR.
    if (this.isBrowser) window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  onPageSizeChanged(size: number): void {
    this.pageSize.set(size);
    this.page.set(0);
    this.loadProducts();
  }

  private loadProducts(): void {
    this.isLoading.set(true);
    this.catalogService
      .getProducts({
        page: this.page(),
        pageSize: this.pageSize(),
        categoryId: this.selectedCategoryId(),
        fts: this.searchText().trim() || null,
      })
      .subscribe({
        next: (result) => {
          this.products.set(result.items);
          this.totalCount.set(result.totalCount);
          this.isLoading.set(false);
        },
        error: () => {
          this.products.set([]);
          this.totalCount.set(0);
          this.isLoading.set(false);
        },
      });
  }

  ngOnDestroy(): void {
    if (this.searchDebounce) clearTimeout(this.searchDebounce);
  }
}
