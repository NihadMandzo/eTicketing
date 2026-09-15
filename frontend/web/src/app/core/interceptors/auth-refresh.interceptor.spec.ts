import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { UserResponse } from '../models/auth.models';
import { AuthService } from '../services/auth.service';
import { authRefreshInterceptor } from './auth-refresh.interceptor';

const AUTH = `${environment.apiBaseUrl}/auth`;

const USER = {
  id: 'c19fb6e7-2c43-4752-c693-08df123f07de',
  firstName: 'Nihad',
  lastName: 'Mandzo',
  email: 'nihad@example.com',
  username: 'nihad',
  roleName: 'User',
} as UserResponse;

const unauthorized = { status: 401, statusText: 'Unauthorized' };

describe('authRefreshInterceptor', () => {
  let httpClient: HttpClient;
  let httpController: HttpTestingController;
  let authService: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authRefreshInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    httpClient = TestBed.inject(HttpClient);
    httpController = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService);
  });

  afterEach(() => httpController.verify());

  it('refreshes and retries a request whose access token had expired', () => {
    let body: unknown;
    httpClient.get('/api/tickets').subscribe((response) => (body = response));

    httpController.expectOne('/api/tickets').flush(null, unauthorized);
    httpController.expectOne(`${AUTH}/refresh`).flush({ user: USER });
    httpController.expectOne('/api/tickets').flush({ items: [] });

    expect(body).toEqual({ items: [] });
    expect(authService.currentUser()).toEqual(USER);
  });

  it('refreshes for /auth/me too — the one call every page load makes to restore the session', () => {
    httpClient.get(`${AUTH}/me`).subscribe();

    httpController.expectOne(`${AUTH}/me`).flush(null, unauthorized);
    httpController.expectOne(`${AUTH}/refresh`).flush({ user: USER });
    httpController.expectOne(`${AUTH}/me`).flush(USER);

    expect(authService.currentUser()).toEqual(USER);
  });

  it('issues ONE refresh for several requests that expire together, not one each', () => {
    // The regression this guards: the backend rotates the refresh token under an atomic
    // compare-and-swap, so of N concurrent refreshes exactly one succeeds and the rest are
    // rejected by design. Firing one per 401 therefore guaranteed failures, and each failure
    // cleared `currentUser` — signing out a visitor whose session had just been renewed.
    httpClient.get('/api/tickets').subscribe();
    httpClient.get('/api/products').subscribe();
    httpClient.get(`${AUTH}/me`).subscribe();

    httpController.expectOne('/api/tickets').flush(null, unauthorized);
    httpController.expectOne('/api/products').flush(null, unauthorized);
    httpController.expectOne(`${AUTH}/me`).flush(null, unauthorized);

    // expectOne throws if the interceptor opened a second or third rotation.
    httpController.expectOne(`${AUTH}/refresh`).flush({ user: USER });

    httpController.expectOne('/api/tickets').flush({ items: [] });
    httpController.expectOne('/api/products').flush({ items: [] });
    httpController.expectOne(`${AUTH}/me`).flush(USER);

    expect(authService.currentUser()).toEqual(USER);
  });

  it('starts a fresh rotation for a later expiry instead of replaying the first result', () => {
    httpClient.get('/api/tickets').subscribe();
    httpController.expectOne('/api/tickets').flush(null, unauthorized);
    httpController.expectOne(`${AUTH}/refresh`).flush({ user: USER });
    httpController.expectOne('/api/tickets').flush({ items: [] });

    // Same session, 15 minutes later: the shared handle must have been released.
    httpClient.get('/api/products').subscribe();
    httpController.expectOne('/api/products').flush(null, unauthorized);
    httpController.expectOne(`${AUTH}/refresh`).flush({ user: USER });
    httpController.expectOne('/api/products').flush({ items: [] });

    expect(authService.currentUser()).toEqual(USER);
  });

  it('signs the user out when the refresh itself fails', () => {
    authService.currentUser.set(USER);
    let failed = false;
    httpClient.get('/api/tickets').subscribe({ error: () => (failed = true) });

    httpController.expectOne('/api/tickets').flush(null, unauthorized);
    httpController.expectOne(`${AUTH}/refresh`).flush(null, unauthorized);

    expect(failed).toBe(true);
    expect(authService.currentUser()).toBeNull();
  });

  it('does not refresh on a failed login — a 401 there means wrong credentials', () => {
    httpClient.post(`${AUTH}/login`, {}).subscribe({ error: () => void 0 });

    httpController.expectOne(`${AUTH}/login`).flush(null, unauthorized);
    httpController.expectNone(`${AUTH}/refresh`);
  });

  it('does not refresh in response to a failing refresh — that would loop', () => {
    httpClient.post(`${AUTH}/refresh`, {}).subscribe({ error: () => void 0 });

    httpController.expectOne(`${AUTH}/refresh`).flush(null, unauthorized);
    httpController.expectNone(`${AUTH}/refresh`);
  });

  it('leaves non-401 failures alone', () => {
    httpClient.get('/api/tickets').subscribe({ error: () => void 0 });

    httpController
      .expectOne('/api/tickets')
      .flush(null, { status: 500, statusText: 'Server Error' });
    httpController.expectNone(`${AUTH}/refresh`);
  });
});
