import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { Category, PagedResult, Product, ProductQuery } from '../models/catalog.models';

@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  getCategories(): Observable<Category[]> {
    return this.http.get<Category[]>(`${this.baseUrl}/categories`);
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
