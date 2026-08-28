import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { City, Product, normalizeProduct } from '../models/catalog.models';
import {
  RecommendationResult,
  toRecommendationSource,
} from '../models/recommendation.models';

@Injectable({ providedIn: 'root' })
export class RecommendationService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/recommendations`;

  /**
   * GET /api/recommendations/me — signed-in visitors only. Never returns an empty list for lack
   * of history: the API falls back to popularity, and reports which strategy it used in `source`.
   */
  getForMe(take = 8): Observable<RecommendationResult> {
    return this.http
      .get<RecommendationResult>(`${this.baseUrl}/me`, { params: { take: String(take) } })
      .pipe(
        map((result) => ({
          items: result.items.map(normalizeProduct),
          source: toRecommendationSource(result.source),
        })),
      );
  }

  /** GET /api/recommendations/similar/{productId} — public, needs no user history. */
  getSimilar(productId: string, take = 6): Observable<Product[]> {
    return this.http
      .get<Product[]>(`${this.baseUrl}/similar/${productId}`, { params: { take: String(take) } })
      .pipe(map((products) => products.map(normalizeProduct)));
  }

  /** GET /api/recommendations/popular — public. What anonymous visitors see in place of a
   * personalized row. */
  getPopular(city?: City | null, take = 8): Observable<Product[]> {
    const params: Record<string, string> = { take: String(take) };
    if (city) params['city'] = city;

    return this.http
      .get<Product[]>(`${this.baseUrl}/popular`, { params })
      .pipe(map((products) => products.map(normalizeProduct)));
  }

  /**
   * POST /api/recommendations/views — fire-and-forget from the caller's point of view. Callers
   * must still subscribe (an unsubscribed HttpClient observable never fires) and must swallow the
   * error: failing to record a view is not something to interrupt someone's browsing over.
   */
  trackView(productId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/views`, { productId });
  }
}
