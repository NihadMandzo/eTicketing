import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/catalog.models';
import { PurchaseRequest, PurchaseResponse, Ticket } from '../models/purchase.models';

@Injectable({ providedIn: 'root' })
export class PurchaseService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  /** POST /api/purchases — the synchronous purchase critical path. */
  purchase(request: PurchaseRequest): Observable<PurchaseResponse> {
    return this.http.post<PurchaseResponse>(`${this.baseUrl}/purchases`, request);
  }

  /** GET /api/tickets/mine — the caller's own tickets, any status. */
  getMyTickets(page = 0, pageSize = 50): Observable<PagedResult<Ticket>> {
    return this.http.get<PagedResult<Ticket>>(`${this.baseUrl}/tickets/mine`, {
      params: { page: String(page), pageSize: String(pageSize) },
    });
  }
}
