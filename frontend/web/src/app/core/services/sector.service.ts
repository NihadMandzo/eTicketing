import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { HoldSectorRequest, HoldSectorResponse, Sector } from '../models/sector.models';
import { PagedResult, toPublishStatus, toTicketingMode } from '../models/catalog.models';

/** Same ordinal-vs-name normalization CatalogService does — see coerceEnum. */
function normalizeSector(raw: Sector): Sector {
  return {
    ...raw,
    ticketingMode: toTicketingMode(raw.ticketingMode),
    status: toPublishStatus(raw.status),
  };
}

@Injectable({ providedIn: 'root' })
export class SectorService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  /** GET /api/sectors?productId= — Published only, public. */
  getSectors(productId: string): Observable<PagedResult<Sector>> {
    return this.http
      .get<PagedResult<Sector>>(`${this.baseUrl}/sectors`, { params: { productId, pageSize: '100' } })
      .pipe(map((result) => ({ ...result, items: result.items.map(normalizeSector) })));
  }

  /** POST /api/sectors/{id}/hold — Redis atomic hold, TTL 5 min. */
  hold(sectorId: string, request: HoldSectorRequest): Observable<HoldSectorResponse> {
    return this.http.post<HoldSectorResponse>(`${this.baseUrl}/sectors/${sectorId}/hold`, request);
  }
}
