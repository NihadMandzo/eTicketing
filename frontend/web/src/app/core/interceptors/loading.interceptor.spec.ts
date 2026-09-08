import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { LoadingService } from '../services/loading.service';
import { SKIP_LOADING_OVERLAY, loadingInterceptor } from './loading.interceptor';

describe('loadingInterceptor', () => {
  let httpClient: HttpClient;
  let httpController: HttpTestingController;
  let loadingService: LoadingService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([loadingInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    httpClient = TestBed.inject(HttpClient);
    httpController = TestBed.inject(HttpTestingController);
    loadingService = TestBed.inject(LoadingService);
  });

  afterEach(() => httpController.verify());

  it('reports loading for the duration of a successful request', () => {
    httpClient.get('/api/products').subscribe();
    expect(loadingService.isLoading()).toBe(true);

    httpController.expectOne('/api/products').flush([]);
    expect(loadingService.isLoading()).toBe(false);
  });

  it('also stops on a failed request — a broken call must not leave the page frozen', () => {
    httpClient.get('/api/products').subscribe({ error: () => void 0 });
    expect(loadingService.isLoading()).toBe(true);

    httpController.expectOne('/api/products').flush('boom', { status: 500, statusText: 'Server Error' });
    expect(loadingService.isLoading()).toBe(false);
  });

  it('stays loading until every overlapping request settles, not just the first one', () => {
    httpClient.get('/api/products').subscribe();
    httpClient.get('/api/categories').subscribe();
    expect(loadingService.isLoading()).toBe(true);

    httpController.expectOne('/api/products').flush([]);
    // One of two outstanding calls just finished — the overlay must not have dropped yet.
    expect(loadingService.isLoading()).toBe(true);

    httpController.expectOne('/api/categories').flush([]);
    expect(loadingService.isLoading()).toBe(false);
  });

  it('stops even when the subscriber unsubscribes before the request settles', () => {
    const subscription = httpClient.get('/api/products').subscribe();
    expect(loadingService.isLoading()).toBe(true);

    subscription.unsubscribe();
    expect(loadingService.isLoading()).toBe(false);

    // Confirms this was a genuine cancellation, not a coincidence — HttpTestingController refuses
    // to flush a request once it has been marked cancelled.
    expect(httpController.expectOne('/api/products').cancelled).toBe(true);
  });

  it('never blocks a request tagged with SKIP_LOADING_OVERLAY', () => {
    httpClient
      .post('/api/recommendations/views', {}, { context: new HttpContext().set(SKIP_LOADING_OVERLAY, true) })
      .subscribe();

    expect(loadingService.isLoading()).toBe(false);

    httpController.expectOne('/api/recommendations/views').flush(null);
    expect(loadingService.isLoading()).toBe(false);
  });
});
