import { HttpClient, HttpHeaders, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { OVERLOAD_MAX_RETRIES, overloadRetryInterceptor } from './overload-retry.interceptor';

const OVERLOADED = {
  status: 503,
  statusText: 'Service Unavailable',
  headers: new HttpHeaders({ 'Retry-After': '2' }),
};
const OVERLOADED_BODY = {
  code: 'server.overloaded',
  message: 'Sistem je trenutno preopterećen. Pokušajte ponovo za nekoliko sekundi.',
};

describe('overloadRetryInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([overloadRetryInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    controller.verify();
    vi.useRealTimers();
  });

  it('retries an overloaded refusal after Retry-After and returns the eventual success', () => {
    let result: unknown;
    http.post('/api/sectors/1/hold', { quantity: 1 }).subscribe((r) => (result = r));

    controller.expectOne('/api/sectors/1/hold').flush(OVERLOADED_BODY, OVERLOADED);
    vi.advanceTimersByTime(1999);
    controller.expectNone('/api/sectors/1/hold');
    vi.advanceTimersByTime(1001); // Retry-After 2 s + up to 1 s of jitter

    controller.expectOne('/api/sectors/1/hold').flush({ holdId: 'h1' });
    expect(result).toEqual({ holdId: 'h1' });
  });

  it(`gives up after ${OVERLOAD_MAX_RETRIES} retries and surfaces the Bosnian message`, () => {
    let error: { status?: number; error?: { message?: string } } | undefined;
    http.get('/api/products').subscribe({ error: (e) => (error = e) });

    for (let attempt = 0; attempt <= OVERLOAD_MAX_RETRIES; attempt++) {
      controller.expectOne('/api/products').flush(OVERLOADED_BODY, OVERLOADED);
      vi.advanceTimersByTime(3000);
    }

    expect(error?.status).toBe(503);
    expect(error?.error?.message).toBe(OVERLOADED_BODY.message);
  });

  it('does not retry a 503 that is not the overload refusal', () => {
    // payment.unavailable: the purchase was processed and its hold released — retrying cannot help.
    let error: { status?: number } | undefined;
    http.post('/api/purchases', {}).subscribe({ error: (e) => (error = e) });

    controller
      .expectOne('/api/purchases')
      .flush({ code: 'payment.unavailable', message: 'Plaćanje trenutno nije dostupno.' }, OVERLOADED);
    vi.advanceTimersByTime(10_000);

    controller.expectNone('/api/purchases');
    expect(error?.status).toBe(503);
  });

  it('does not retry other errors', () => {
    let error: { status?: number } | undefined;
    http.post('/api/sectors/1/hold', {}).subscribe({ error: (e) => (error = e) });

    controller
      .expectOne('/api/sectors/1/hold')
      .flush({ code: 'sector.no_capacity', message: 'Nema dovoljno slobodnog kapaciteta.' }, { status: 409, statusText: 'Conflict' });
    vi.advanceTimersByTime(10_000);

    controller.expectNone('/api/sectors/1/hold');
    expect(error?.status).toBe(409);
  });

  it('waits 2 seconds when Retry-After is missing', () => {
    http.get('/api/categories').subscribe();

    controller.expectOne('/api/categories').flush(OVERLOADED_BODY, { status: 503, statusText: 'Service Unavailable' });
    vi.advanceTimersByTime(1999);
    controller.expectNone('/api/categories');
    vi.advanceTimersByTime(1001);

    controller.expectOne('/api/categories').flush([]);
  });
});
