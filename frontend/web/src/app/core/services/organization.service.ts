import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { Organization } from '../models/organization.models';

/** GET /api/organizations/{id}/public is the anonymous storefront read (see
 * OrganizationEndpoints) — it returns the organization's published business details only.
 * The unsuffixed /organizations/{id} now requires a session and carries back-office fields
 * (staff count, active flag, created date) that this app has no use for. */
@Injectable({ providedIn: 'root' })
export class OrganizationService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  getById(id: string): Observable<Organization> {
    return this.http.get<Organization>(`${this.baseUrl}/organizations/${id}/public`);
  }
}
