import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/catalog.models';
import { PurchaseRequest, PurchaseResponse, Ticket, toTicketStatus } from '../models/purchase.models';

/** Ticket.status is an ordinal on the wire, same as every other enum — see
 * coerceEnum. Left un-normalized it renders as a bare "1" in the status
 * chip on both the profile list and the ticket detail modal. */
function normalizeTicket(raw: Ticket): Ticket {
  return { ...raw, status: toTicketStatus(raw.status) };
}

@Injectable({ providedIn: 'root' })
export class PurchaseService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  /** POST /api/purchases — the synchronous purchase critical path. */
  purchase(request: PurchaseRequest): Observable<PurchaseResponse> {
    return this.http
      .post<PurchaseResponse>(`${this.baseUrl}/purchases`, request)
      .pipe(map((result) => ({ ...result, tickets: result.tickets.map(normalizeTicket) })));
  }

  /** GET /api/tickets/mine — the caller's own tickets, any status. */
  getMyTickets(page = 0, pageSize = 50): Observable<PagedResult<Ticket>> {
    return this.http
      .get<PagedResult<Ticket>>(`${this.baseUrl}/tickets/mine`, {
        params: { page: String(page), pageSize: String(pageSize) },
      })
      .pipe(map((result) => ({ ...result, items: result.items.map(normalizeTicket) })));
  }
}
