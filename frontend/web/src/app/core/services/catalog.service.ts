import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { Category, PagedResult, Product, ProductQuery } from '../models/catalog.models';

@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  /**
   * GET /api/categories — like /api/products, this always comes back as a
   * PagedResult wrapper (`{items, totalCount, page, pageSize}`), never a
   * bare array. This used to be typed `Observable<Category[]>` straight off
   * `http.get`, which is only a compile-time assertion — at runtime the
   * signal fed by this call held the raw wrapper object, which `@for` in
   * category-chips can't iterate. pageSize is set to the server's enforced
   * ceiling since there are only ever a handful of categories and this call
   * should just return all of them, not the default page size of 10.
   */
  getCategories(): Observable<Category[]> {
    return this.http
      .get<PagedResult<Category>>(`${this.baseUrl}/categories`, { params: { pageSize: '100' } })
      .pipe(map((result) => result.items));
  }

  /** GET /api/products — Published only, public. */
  getProducts(query?: ProductQuery): Observable<PagedResult<Product>> {
    const params: Record<string, string> = {};
    if (query?.page !== undefined) params['page'] = String(query.page);
    if (query?.pageSize !== undefined) params['pageSize'] = String(query.pageSize);
    if (query?.fts) params['fts'] = query.fts;
    if (query?.categoryId != null) params['categoryId'] = String(query.categoryId);

    return this.http.get<PagedResult<Product>>(`${this.baseUrl}/products`, { params });
  }

  /** GET /api/products/{id} — Published only, public (404 for Draft/unknown). */
  getProductById(id: string): Observable<Product> {
    return this.http.get<Product>(`${this.baseUrl}/products/${id}`);
  }
}
