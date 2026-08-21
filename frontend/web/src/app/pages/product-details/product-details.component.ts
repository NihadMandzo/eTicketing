import { DatePipe, isPlatformBrowser } from '@angular/common';
import { ChangeDetectionStrategy, Component, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { GoogleMap, MapMarker } from '@angular/google-maps';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

import { environment } from '../../../environments/environment';
import { CatalogService } from '../../core/services/catalog.service';
import { SectorService } from '../../core/services/sector.service';
import { OrganizationService } from '../../core/services/organization.service';
import { CartHoldGroup, CartService } from '../../core/services/cart.service';
import { CITY_LABELS, Product } from '../../core/models/catalog.models';
import { Sector } from '../../core/models/sector.models';
import { Organization } from '../../core/models/organization.models';

/** One purchasable row on the SingleOccurrence/DailyEntry purchase card — one
 * per Sector.TicketType, or one per Sector itself when it has no TicketTypes
 * (today's single-implicit-price case). */
interface PurchaseRow {
  sectorId: string;
  sectorName: string;
  ticketTypeId: string | null;
  ticketTypeName: string | null;
  price: number;
}

function rowKey(sectorId: string, ticketTypeId: string | null): string {
  return `${sectorId}::${ticketTypeId ?? 'flat'}`;
}

function tomorrow(): Date {
  const date = new Date();
  date.setHours(0, 0, 0, 0);
  date.setDate(date.getDate() + 1);
  return date;
}

/** Local-time `yyyy-MM-dd`. Deliberately not `toISOString().slice(0, 10)`,
 * which shifts to UTC and hands the backend the previous day for anyone east
 * of Greenwich — including Sarajevo. */
function toIsoDate(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

@Component({
  selector: 'app-product-details',
  imports: [RouterLink, DatePipe, GoogleMap, MapMarker],
  templateUrl: './product-details.component.html',
  styleUrl: './product-details.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductDetailsComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly catalogService = inject(CatalogService);
  private readonly sectorService = inject(SectorService);
  private readonly organizationService = inject(OrganizationService);
  private readonly cartService = inject(CartService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly cityLabels = CITY_LABELS;

  // The Maps JS API is loaded lazily, browser-only (SSR has no `window`/`document` to inject a
  // <script> tag into, and there's nothing to render server-side anyway) — <google-map> only
  // renders once this flips true, see loadGoogleMapsScript().
  readonly mapsReady = signal(false);
  readonly mapZoom = 15;
  readonly mapOptions: google.maps.MapOptions = { disableDefaultUI: true, zoomControl: true, clickableIcons: false };

  readonly product = signal<Product | null>(null);
  readonly sectors = signal<Sector[]>([]);
  readonly organization = signal<Organization | null>(null);
  readonly isLoading = signal(true);
  readonly loadError = signal<string | null>(null);

  readonly activeImageIndex = signal(0);

  // SingleOccurrence/DailyEntry: quantities keyed by rowKey().
  readonly quantities = signal<Record<string, number>>({});
  // DailyEntry only.
  readonly selectedDate = signal<string>(this.tomorrowIso());
  // RecurringReservation only.
  readonly selectedSpotSectorId = signal<string | null>(null);
  readonly takenSpotIds = signal<Set<string>>(new Set());
  // The Redis holdId behind the currently-selected spot, so switching to a different spot can
  // release this one instead of leaking it for the full 5-minute TTL.
  private selectedSpotHoldId: string | null = null;

  readonly isSubmitting = signal(false);
  readonly submitError = signal<string | null>(null);

  readonly minDate = this.tomorrowIso();

  /**
   * A DailyEntry sector defines capacity+price for one calendar month
   * (PeriodYear/PeriodMonth), so only the sectors covering the chosen date's
   * month are purchasable for it — the backend rejects anything else with
   * `sector.date_out_of_period` (see SectorService.HoldAsync). Flattening
   * every period's sectors into one list, as this used to, meant a museum
   * with a September *and* an October sector offered both at once and then
   * failed the hold for whichever one didn't match.
   */
  readonly sectorsForSelectedDate = computed<Sector[]>(() => {
    if (this.product()?.ticketingMode !== 'DailyEntry') return this.sectors();
    const [year, month] = this.selectedDate().split('-').map(Number);
    return this.sectors().filter((sector) => sector.periodYear === year && sector.periodMonth === month);
  });

  readonly rows = computed<PurchaseRow[]>(() =>
    this.sectorsForSelectedDate().flatMap((sector): PurchaseRow[] =>
      sector.ticketTypes.length > 0
        ? sector.ticketTypes.map((tt) => ({
            sectorId: sector.id,
            sectorName: sector.name,
            ticketTypeId: tt.id,
            ticketTypeName: tt.name,
            price: tt.price,
          }))
        : [{ sectorId: sector.id, sectorName: sector.name, ticketTypeId: null, ticketTypeName: null, price: sector.price }],
    ),
  );

  readonly total = computed(() => {
    const quantities = this.quantities();
    return this.rows().reduce((sum, row) => sum + (quantities[rowKey(row.sectorId, row.ticketTypeId)] ?? 0) * row.price, 0);
  });

  readonly hasAnySelection = computed(() => {
    const quantities = this.quantities();
    return Object.values(quantities).some((q) => q > 0);
  });

  readonly selectedSpotTotal = computed<number | null>(() => {
    const spotId = this.selectedSpotSectorId();
    if (!spotId) return null;
    return this.sectors().find((s) => s.id === spotId)?.price ?? null;
  });

  readonly mapCenter = computed<google.maps.LatLngLiteral | null>(() => {
    const product = this.product();
    if (!product) return null;
    return { lat: product.latitude, lng: product.longitude };
  });

  constructor() {
    if (this.isBrowser) this.loadGoogleMapsScript();

    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.loadError.set('Proizvod nije pronađen.');
      this.isLoading.set(false);
      return;
    }

    forkJoin({
      product: this.catalogService.getProductById(id),
      sectors: this.sectorService.getSectors(id),
    }).subscribe({
      next: ({ product, sectors }) => {
        this.product.set(product);
        this.sectors.set(sectors.items);
        if (product.ticketingMode === 'DailyEntry') {
          this.selectedDate.set(this.firstSelectableDate(sectors.items));
        }
        this.isLoading.set(false);
        this.loadOrganization(product.organizationId);
      },
      error: () => {
        this.loadError.set('Proizvod nije pronađen ili više nije dostupan.');
        this.isLoading.set(false);
      },
    });
  }

  /** Organizer name is a nice-to-have — it must never block or fail the
   * whole page, so it's a separate request that swallows its own error
   * (same treatment as mobile's EventDetailsScreen). */
  private loadOrganization(organizationId: string): void {
    this.organizationService
      .getById(organizationId)
      .pipe(catchError(() => of(null)))
      .subscribe((organization) => this.organization.set(organization));
  }

  private tomorrowIso(): string {
    return toIsoDate(tomorrow());
  }

  /** Loads the Google Maps JS API script exactly once per page (shared across every
   * product-details view a buyer navigates to in one session), then flips mapsReady so the
   * @if in the template renders <google-map>. Dynamic <script> injection, rather than a static
   * tag in index.html, because the API key comes from environment.ts — a compile-time constant,
   * not read from the Dockerfile's GOOGLE_MAPS_API_KEY build arg (that arg is reserved for future
   * wiring, same "no-op today" status as WEB_API_BASE_URL) — and index.html has no template step
   * to read it from either way. */
  private loadGoogleMapsScript(): void {
    if (typeof google !== 'undefined' && google.maps) {
      this.mapsReady.set(true);
      return;
    }
    const existing = document.getElementById('google-maps-script') as HTMLScriptElement | null;
    if (existing) {
      existing.addEventListener('load', () => this.mapsReady.set(true));
      return;
    }
    const script = document.createElement('script');
    script.id = 'google-maps-script';
    script.src = `https://maps.googleapis.com/maps/api/js?key=${environment.googleMapsApiKey}`;
    script.async = true;
    script.onload = () => this.mapsReady.set(true);
    document.head.appendChild(script);
  }

  /** Earliest bookable day: tomorrow if that already falls inside a sector's
   * period, otherwise day 1 of the earliest period still ahead of us.
   * Without this the picker opened on tomorrow, which for a museum whose
   * sectors start next month meant an empty ticket list and no way to tell
   * why. */
  private firstSelectableDate(sectors: Sector[]): string {
    const earliest = tomorrow();
    const periods = sectors
      .filter((sector) => sector.periodYear != null && sector.periodMonth != null)
      .map((sector) => ({ year: sector.periodYear!, month: sector.periodMonth! }))
      .sort((a, b) => a.year - b.year || a.month - b.month);

    for (const period of periods) {
      const lastDay = new Date(period.year, period.month, 0);
      if (lastDay < earliest) continue;
      const firstDay = new Date(period.year, period.month - 1, 1);
      return toIsoDate(firstDay > earliest ? firstDay : earliest);
    }
    return toIsoDate(earliest);
  }

  /** Latest day any sector covers — caps the native date picker so a buyer
   * can't wander months past the last published period. */
  readonly maxDate = computed<string | null>(() => {
    const periods = this.sectors().filter((s) => s.periodYear != null && s.periodMonth != null);
    if (periods.length === 0) return null;
    const last = periods.reduce((a, b) =>
      b.periodYear! > a.periodYear! || (b.periodYear === a.periodYear && b.periodMonth! > a.periodMonth!) ? b : a,
    );
    return toIsoDate(new Date(last.periodYear!, last.periodMonth!, 0));
  });


  selectImage(index: number): void {
    this.activeImageIndex.set(index);
  }

  quantityFor(row: PurchaseRow): number {
    return this.quantities()[rowKey(row.sectorId, row.ticketTypeId)] ?? 0;
  }

  incQuantity(row: PurchaseRow): void {
    const key = rowKey(row.sectorId, row.ticketTypeId);
    this.quantities.update((q) => ({ ...q, [key]: Math.min(10, (q[key] ?? 0) + 1) }));
  }

  decQuantity(row: PurchaseRow): void {
    const key = rowKey(row.sectorId, row.ticketTypeId);
    this.quantities.update((q) => ({ ...q, [key]: Math.max(0, (q[key] ?? 0) - 1) }));
  }

  onDateChange(value: string): void {
    if (!value) return;
    const previous = this.selectedDate();
    this.selectedDate.set(value);
    // A different month means a different set of sectors, so the quantities
    // keyed by the old sectors' ids no longer refer to anything on screen.
    if (previous.slice(0, 7) !== value.slice(0, 7)) {
      this.quantities.set({});
      this.submitError.set(null);
    }
  }

  /** SingleOccurrence/DailyEntry — places one hold per Sector that has any
   * selected quantity (a Redis hold is scoped to a single Sector), then
   * routes to Checkout with all of them bundled as one cart. */
  proceedToCheckout(): void {
    const product = this.product();
    if (!product || this.isSubmitting()) return;

    const quantities = this.quantities();
    const bySector = new Map<string, PurchaseRow[]>();
    for (const row of this.rows()) {
      if ((quantities[rowKey(row.sectorId, row.ticketTypeId)] ?? 0) > 0) {
        bySector.set(row.sectorId, [...(bySector.get(row.sectorId) ?? []), row]);
      }
    }
    if (bySector.size === 0) return;

    const isDailyEntry = product.ticketingMode === 'DailyEntry';
    this.isSubmitting.set(true);
    this.submitError.set(null);

    // Every hold call is caught individually rather than left to forkJoin's own all-or-nothing
    // failure: with plain forkJoin, one Sector's hold failing (e.g. sold out between page load and
    // click) errors the whole subscription without ever telling us which OTHER holds it already
    // placed — those would then sit as leaked, silently-ticking Redis holds with no way for this
    // component to release them. Catching per-call turns every branch into a settled outcome, so we
    // can release whichever holds actually succeeded before reporting the failure.
    const holdCalls = Array.from(bySector.entries()).map(([sectorId, sectorRows]) => {
      const quantity = sectorRows.reduce((sum, row) => sum + this.quantityFor(row), 0);
      return this.sectorService.hold(sectorId, { quantity, date: isDailyEntry ? this.selectedDate() : null }).pipe(
        map((response) => ({ ok: true as const, sectorId, sectorRows, response })),
        catchError((error: unknown) => of({ ok: false as const, sectorId, sectorRows, error })),
      );
    });

    forkJoin(holdCalls).subscribe((outcomes) => {
      const failed = outcomes.find((o) => !o.ok);
      if (failed) {
        // Give back capacity for every Sector that DID succeed — otherwise those holds just sit
        // there ticking down their own TTL instead of being released immediately.
        for (const outcome of outcomes) {
          if (outcome.ok) this.sectorService.release(outcome.response.holdId).subscribe();
        }
        this.isSubmitting.set(false);
        if (this.isUnauthorized(failed.error)) {
          this.router.navigateByUrl('/prijava');
          return;
        }
        this.submitError.set(this.extractErrorMessage(failed.error));
        return;
      }

      const holds: CartHoldGroup[] = outcomes
        .filter((o): o is Extract<typeof o, { ok: true }> => o.ok)
        .map(({ sectorId, sectorRows, response }) => ({
          holdId: response.holdId,
          sectorId,
          sectorName: sectorRows[0].sectorName,
          lineItems: sectorRows.map((row) => ({
            ticketTypeId: row.ticketTypeId,
            ticketTypeName: row.ticketTypeName,
            quantity: this.quantityFor(row),
            unitPrice: row.price,
          })),
        }));

      this.cartService.set({
        productId: product.id,
        productName: product.name,
        date: isDailyEntry ? this.selectedDate() : null,
        holds,
      });
      this.isSubmitting.set(false);
      this.router.navigateByUrl('/placanje');
    });
  }

  /** RecurringReservation — clicking a space attempts a live 1-quantity hold
   * immediately (D3: no fabricated spatial layout, taken/free is a real
   * hold attempt against the same mechanism every purchase path uses). */
  selectSpot(sector: Sector): void {
    if (this.isSubmitting() || this.takenSpotIds().has(sector.id)) return;

    this.isSubmitting.set(true);
    this.submitError.set(null);
    this.sectorService.hold(sector.id, { quantity: 1 }).subscribe({
      next: (hold) => {
        this.isSubmitting.set(false);
        // Only release the previous spot's hold once the new one has actually succeeded — releasing
        // it up front and then having this call fail would leave the buyer with no held spot at all.
        const previousHoldId = this.selectedSpotHoldId;
        if (previousHoldId) this.sectorService.release(previousHoldId).subscribe();

        this.selectedSpotHoldId = hold.holdId;
        this.selectedSpotSectorId.set(sector.id);
        const product = this.product()!;
        this.cartService.set({
          productId: product.id,
          productName: product.name,
          date: null,
          holds: [
            {
              holdId: hold.holdId,
              sectorId: sector.id,
              sectorName: sector.name,
              lineItems: [{ ticketTypeId: null, ticketTypeName: null, quantity: 1, unitPrice: sector.price }],
            },
          ],
        });
      },
      error: (error: unknown) => {
        this.isSubmitting.set(false);
        if (this.isUnauthorized(error)) {
          this.router.navigateByUrl('/prijava');
        } else if (this.isConflict(error)) {
          this.takenSpotIds.update((set) => new Set(set).add(sector.id));
          this.submitError.set('Ovo mjesto je upravo zauzeto. Odaberite drugo.');
        } else {
          this.submitError.set(this.extractErrorMessage(error));
        }
      },
    });
  }

  goToCheckoutWithSpot(): void {
    if (this.selectedSpotSectorId()) this.router.navigateByUrl('/placanje');
  }

  private isConflict(error: unknown): boolean {
    return (error as { status?: number })?.status === 409;
  }

  private isUnauthorized(error: unknown): boolean {
    return (error as { status?: number })?.status === 401;
  }

  private extractErrorMessage(error: unknown): string {
    const httpError = error as { error?: { message?: string } };
    return httpError?.error?.message ?? 'Došlo je do greške. Pokušajte ponovo.';
  }
}
