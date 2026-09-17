import { HttpHandler, HttpRequest } from '@angular/common/http';
import { of } from 'rxjs';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { SsrApiBaseInterceptor } from './ssr-api-base.interceptor';

/**
 * The failure this guards against is quiet: if the rewrite stops happening, HttpClient rejects the
 * relative URL on the server, the three per-request SSR routes render with no data, and the page
 * still looks correct to a human once the browser hydrates and re-fetches. Only a crawler — or a
 * slow connection — sees the empty version.
 */
describe('SsrApiBaseInterceptor', () => {
  const originalGatewayUrl = process.env['GATEWAY_INTERNAL_URL'];

  afterEach(() => {
    if (originalGatewayUrl === undefined) {
      delete process.env['GATEWAY_INTERNAL_URL'];
    } else {
      process.env['GATEWAY_INTERNAL_URL'] = originalGatewayUrl;
    }
  });

  /** Captures the request the interceptor passes on, after any rewriting. */
  function forward(url: string): { handled: HttpRequest<unknown>; interceptor: SsrApiBaseInterceptor } {
    // Constructed inside the helper, not in a beforeEach: the gateway address is resolved in a
    // field initializer, so a test that changes the environment has to build its own instance.
    const interceptor = new SsrApiBaseInterceptor();
    let handled!: HttpRequest<unknown>;
    const next: HttpHandler = {
      handle: vi.fn((req: HttpRequest<unknown>) => {
        handled = req;
        return of();
      }),
    };

    interceptor.intercept(new HttpRequest('GET', url), next).subscribe();
    return { handled, interceptor };
  }

  it('rewrites a relative /api URL to the configured gateway', () => {
    process.env['GATEWAY_INTERNAL_URL'] = 'http://gateway.internal:9000';

    const { handled } = forward('/api/products?page=0&pageSize=10');

    expect(handled.url).toBe('http://gateway.internal:9000/api/products?page=0&pageSize=10');
  });

  it('falls back to the compose service name when no gateway address is configured', () => {
    // What makes `docker compose up` work with no extra configuration. Must stay in step with the
    // same default in src/server.ts.
    delete process.env['GATEWAY_INTERNAL_URL'];

    const { handled } = forward('/api/categories');

    expect(handled.url).toBe('http://gateway:8080/api/categories');
  });

  it('leaves a request that is already absolute alone', () => {
    // Anything not starting with the API base — a Google Maps script, say. Prefixing it would
    // produce a nonsense URL rather than an obvious failure.
    process.env['GATEWAY_INTERNAL_URL'] = 'http://gateway.internal:9000';

    const { handled } = forward('https://maps.googleapis.com/maps/api/js');

    expect(handled.url).toBe('https://maps.googleapis.com/maps/api/js');
  });

  it('does not prefix a URL twice', () => {
    // Guards the shape of the check itself: a rewritten URL contains "/api" but does not *start*
    // with it, so a contains-style test here would corrupt every retried request.
    process.env['GATEWAY_INTERNAL_URL'] = 'http://gateway.internal:9000';

    const { handled } = forward('http://gateway.internal:9000/api/products');

    expect(handled.url).toBe('http://gateway.internal:9000/api/products');
  });
});
