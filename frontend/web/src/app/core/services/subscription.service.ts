import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/catalog.models';
import { Subscription, toSubscriptionStatus } from '../models/subscription.models';

@Injectable({ providedIn: 'root' })
export class SubscriptionService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  /** GET /api/subscriptions/mine — the caller's own recurring reservations. */
  getMine(page = 0, pageSize = 10): Observable<PagedResult<Subscription>> {
    return this.http
      .get<PagedResult<Subscription>>(`${this.baseUrl}/subscriptions/mine`, {
        params: { page: String(page), pageSize: String(pageSize) },
      })
      .pipe(
        map((result) => ({
          ...result,
          // status is an ordinal on the wire, same as every other enum — left un-normalized it
          // renders as a bare "0" in the status chip.
          items: result.items.map((s) => ({ ...s, status: toSubscriptionStatus(s.status) })),
        })),
      );
  }

  /**
   * POST /api/subscriptions/{id}/cancel — schedules cancellation at the end of the paid-for period.
   * The subscription stays Active until the provider confirms it has actually ended, so the buyer
   * keeps the month they already paid for.
   */
  cancel(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/subscriptions/${id}/cancel`, {});
  }
}
