import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { CatalogService } from '../../core/services/catalog.service';
import { RecommendationService } from '../../core/services/recommendation.service';
import { AuthService } from '../../core/services/auth.service';
import { Category, Product } from '../../core/models/catalog.models';
import { RECOMMENDATION_TITLES } from '../../core/models/recommendation.models';
import { CategoryCardsComponent } from '../../components/category-cards/category-cards.component';
import { ProductCardComponent } from '../../components/product-card/product-card.component';
import { RecommendationRowComponent } from '../../components/recommendation-row/recommendation-row.component';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [RouterLink, CategoryCardsComponent, ProductCardComponent, RecommendationRowComponent],
  templateUrl: './home.component.html',
  styleUrl: './home.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomeComponent {
  private readonly catalogService = inject(CatalogService);
  private readonly recommendationService = inject(RecommendationService);
  private readonly authService = inject(AuthService);

  readonly categories = signal<Category[]>([]);
  readonly products = signal<Product[]>([]);
  readonly selectedCategoryId = signal<number | null>(null);
  readonly isLoading = signal(true);

  readonly recommendations = signal<Product[]>([]);
  readonly recommendationTitle = signal<string>(RECOMMENDATION_TITLES.Popular);
  readonly isLoadingRecommendations = signal(true);

  constructor() {
    this.catalogService.getCategories().subscribe({
      next: (categories) => {
        this.categories.set(categories);
        // No "Sve kategorije" catch-all here (see CategoryCardsComponent) — the first real
        // category is the default selection, lightly highlighted in the card grid.
        this.selectedCategoryId.set(categories[0]?.id ?? null);
        this.loadProducts();
      },
      error: () => {
        this.categories.set([]);
        this.loadProducts();
      },
    });

    this.loadRecommendations();
  }

  onCategorySelected(categoryId: number): void {
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

  /**
   * Signed-in visitors get the personalized endpoint, which reports back which strategy it
   * actually used so the heading can be honest. Anonymous visitors get the public popularity list
   * instead — the same row, without pretending it is about them.
   *
   * Any failure hides the row rather than showing an error: recommendations are an enhancement to
   * the landing page, and a broken one should cost the visitor nothing.
   */
  private loadRecommendations(): void {
    this.isLoadingRecommendations.set(true);

    if (this.authService.isAuthenticated()) {
      this.recommendationService.getForMe(6).subscribe({
        next: (result) => {
          this.recommendations.set(result.items);
          this.recommendationTitle.set(RECOMMENDATION_TITLES[result.source]);
          this.isLoadingRecommendations.set(false);
        },
        error: () => this.clearRecommendations(),
      });
      return;
    }

    this.recommendationService.getPopular(null, 6).subscribe({
      next: (products) => {
        this.recommendations.set(products);
        this.recommendationTitle.set(RECOMMENDATION_TITLES.Popular);
        this.isLoadingRecommendations.set(false);
      },
      error: () => this.clearRecommendations(),
    });
  }

  private clearRecommendations(): void {
    this.recommendations.set([]);
    this.isLoadingRecommendations.set(false);
  }
}
