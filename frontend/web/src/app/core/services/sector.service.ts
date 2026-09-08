import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';

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

  /**
   * GET /api/sectors?productId= — Published only, public.
   *
   * `date` (`yyyy-MM-dd`) is what makes `remainingCapacity` meaningful for a DailyEntry product:
   * that mode counts capacity per `(sector, date)`, so the backend returns null rather than guess
   * when no date is supplied. Ignored by every other mode. Callers showing a date picker should
   * re-request on each change, since the availability answer changes with it.
   */
  getSectors(productId: string, date?: string | null): Observable<PagedResult<Sector>> {
    const params: Record<string, string> = { productId, pageSize: '100' };
    if (date) params['date'] = date;

    return this.http
      .get<PagedResult<Sector>>(`${this.baseUrl}/sectors`, { params })
      .pipe(map((result) => ({ ...result, items: result.items.map(normalizeSector) })));
  }

  /** POST /api/sectors/{id}/hold — Redis atomic hold, TTL 5 min. */
  hold(sectorId: string, request: HoldSectorRequest): Observable<HoldSectorResponse> {
    return this.http.post<HoldSectorResponse>(`${this.baseUrl}/sectors/${sectorId}/hold`, request);
  }

  /** POST /api/sectors/holds/{holdId}/release — best-effort early release of a still-live hold
   * (e.g. the buyer switched to a different spot/date, or a sibling hold in the same cart failed
   * to purchase). Always resolves, even on a network/server error — callers only ever want to
   * give capacity back on a best-effort basis, never to block on it or surface a failure toast for
   * a release the buyer didn't directly ask for. */
  release(holdId: string): Observable<void> {
    return this.http
      .post<void>(`${this.baseUrl}/sectors/holds/${holdId}/release`, {})
      .pipe(catchError(() => of(void 0)));
  }
}
