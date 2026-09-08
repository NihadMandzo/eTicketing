import { LoadingService } from './loading.service';

/**
 * `isLoading` has to reflect "is anything outstanding", not "did the most recent request finish" —
 * this app fires several requests at once (a product page's product/sectors/organization/similar
 * calls), so a boolean cleared by the first one to land would drop the overlay while others were
 * still in flight. These tests are really about the counter surviving that overlap.
 */
describe('LoadingService', () => {
  let service: LoadingService;

  beforeEach(() => {
    service = new LoadingService();
  });

  it('starts idle', () => {
    expect(service.isLoading()).toBe(false);
    expect(service.pendingCount()).toBe(0);
  });

  it('reports loading for as long as one request is outstanding', () => {
    service.start();
    expect(service.isLoading()).toBe(true);
    expect(service.pendingCount()).toBe(1);

    service.stop();
    expect(service.isLoading()).toBe(false);
    expect(service.pendingCount()).toBe(0);
  });

  it('stays loading until every overlapping request has stopped', () => {
    service.start();
    service.start();
    service.start();
    expect(service.pendingCount()).toBe(3);

    service.stop();
    expect(service.isLoading()).toBe(true);
    service.stop();
    expect(service.isLoading()).toBe(true);
    service.stop();
    expect(service.isLoading()).toBe(false);
  });

  it('clamps at zero rather than going negative on a stray unbalanced stop()', () => {
    service.stop();
    service.stop();
    expect(service.pendingCount()).toBe(0);
    expect(service.isLoading()).toBe(false);

    // A hole here would otherwise force two extra start() calls before isLoading() ever went true
    // again — one real request should be enough.
    service.start();
    expect(service.isLoading()).toBe(true);
  });
});
