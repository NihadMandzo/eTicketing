import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { CatalogService } from '../../core/services/catalog.service';
import { Category, Product } from '../../core/models/catalog.models';
import { CategoryChipsComponent } from '../../components/category-chips/category-chips.component';
import { ProductCardComponent } from '../../components/product-card/product-card.component';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [RouterLink, CategoryChipsComponent, ProductCardComponent],
  templateUrl: './home.component.html',
  styleUrl: './home.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomeComponent {
  private readonly catalogService = inject(CatalogService);

  readonly categories = signal<Category[]>([]);
  readonly products = signal<Product[]>([]);
  readonly selectedCategoryId = signal<number | null>(null);
  readonly isLoading = signal(true);

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

  private loadProducts(): void {
    this.isLoading.set(true);
    this.catalogService
      .getProducts({ page: 0, pageSize: 6, categoryId: this.selectedCategoryId() })
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
