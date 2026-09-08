import { DatePipe, isPlatformBrowser } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
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
import { Sector, isSoldOut } from '../../core/models/sector.models';
import { Organization } from '../../core/models/organization.models';
import { RecommendationService } from '../../core/services/recommendation.service';
import { AuthService } from '../../core/services/auth.service';
import { RecommendationRowComponent } from '../../components/recommendation-row/recommendation-row.component';

/** Hard cap per row regardless of how much capacity is left — a storefront limit, not a
 * capacity one, so a single buyer can't take a whole small sector in one click. */
const MAX_PER_ORDER = 10;

/** One purchasable row on the SingleOccurrence/DailyEntry purchase card — one
 * per Sector.TicketType, or one per Sector itself when it has no TicketTypes
 * (today's single-implicit-price case). */
interface PurchaseRow {
  sectorId: string;
  sectorName: string;
  ticketTypeId: string | null;
  ticketTypeName: string | null;
  price: number;
  /** Live remaining capacity of the row's *sector*, not of the row: named ticket types share one
   * pool, so three tiers of a 40-seat sector each show 40, and buying any of them draws that down
   * for all three. Null means the backend could not state it — never render null as sold out. */
  remaining: number | null;
  soldOut: boolean;
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
  imports: [RouterLink, DatePipe, GoogleMap, MapMarker, RecommendationRowComponent],
  templateUrl: './product-details.component.html',
  styleUrl: './product-details.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductDetailsComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly catalogService = inject(CatalogService);
  private readonly sectorService = inject(SectorService);
  private readonly organizationService = inject(OrganizationService);
  private readonly cartService = inject(CartService);
  private readonly recommendationService = inject(RecommendationService);
  private readonly authService = inject(AuthService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly cityLabels = CITY_LABELS;

  /** Stand-in for a missing organizer logo — the first letter of each of the
   * first two words, so "Sunset Events d.o.o." reads as "SE". */
  organizerInitials(name: string): string {
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((word) => word[0].toUpperCase())
      .join('');
  }

  // The Maps JS API is loaded lazily, browser-only (SSR has no `window`/`document` to inject a
  // <script> tag into, and there's nothing to render server-side anyway) — <google-map> only
  // renders once this flips true, see loadGoogleMapsScript().
  readonly mapsReady = signal(false);
  /** No key configured in `.env` → the script is never requested at all. Loading it keyless just
   * buys Google's watermarked grey tile, which reads as a broken map rather than an unconfigured
   * one; the template shows a plain "map unavailable" panel instead. */
  readonly hasMapsKey = environment.googleMapsApiKey.trim().length > 0;
  readonly mapZoom = 15;
  readonly mapOptions: google.maps.MapOptions = { disableDefaultUI: true, zoomControl: true, clickableIcons: false };

  readonly product = signal<Product | null>(null);
  readonly sectors = signal<Sector[]>([]);
  readonly organization = signal<Organization | null>(null);
  readonly isLoading = signal(true);
  readonly loadError = signal<string | null>(null);

  readonly similarProducts = signal<Product[]>([]);
  readonly isLoadingSimilar = signal(true);

  readonly activeImageIndex = signal(0);

  /** "O ponudi" starts clamped. A description can be several screens long, and at full height it
   * pushes the location, organizer and everything below it off the page — so it opens as a fixed
   * block with a "Prikaži više" toggle rather than running on. */
  readonly isDescriptionExpanded = signal(false);

  // SingleOccurrence/DailyEntry: quantities keyed by rowKey().
  readonly quantities = signal<Record<string, number>>({});
  // DailyEntry only.
  readonly selectedDate = signal<string>(this.tomorrowIso());
  /** DailyEntry re-requests its sectors whenever the date changes, because availability is per
   * `(sector, date)` — this covers that in-flight window so the card doesn't flash "sold out". */
  readonly isReloadingAvailability = signal(false);
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
    this.sectorsForSelectedDate().flatMap((sector): PurchaseRow[] => {
      const availability = { remaining: sector.remainingCapacity, soldOut: isSoldOut(sector) };
      return sector.ticketTypes.length > 0
        ? sector.ticketTypes.map((tt) => ({
            sectorId: sector.id,
            sectorName: sector.name,
            ticketTypeId: tt.id,
            ticketTypeName: tt.name,
            price: tt.price,
            ...availability,
          }))
        : [
            {
              sectorId: sector.id,
              sectorName: sector.name,
              ticketTypeId: null,
              ticketTypeName: null,
              price: sector.price,
              ...availability,
            },
          ];
    }),
  );

  /** Every row is exhausted — the card says so once, up top, instead of repeating "Rasprodano" on
   * each line and still showing a live "Kupi" button underneath. */
  readonly allSoldOut = computed(() => this.rows().length > 0 && this.rows().every((row) => row.soldOut));

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

  /** A parking space is unavailable either because the live counter says so or because a hold
   * attempt just lost the race — one predicate so the grid and the click handler can't disagree. */
  spotUnavailable(sector: Sector): boolean {
    return isSoldOut(sector) || this.takenSpotIds().has(sector.id);
  }

  readonly availableSpotCount = computed(() => this.sectors().filter((s) => !this.spotUnavailable(s)).length);

  readonly mapCenter = computed<google.maps.LatLngLiteral | null>(() => {
    const product = this.product();
    if (!product) return null;
    return { lat: product.latitude, lng: product.longitude };
  });

  constructor() {
    if (this.isBrowser && this.hasMapsKey) this.loadGoogleMapsScript();

    // Subscribed, not read once off `snapshot`. The router reuses this component for every
    // `/dogadjaji/:id` navigation, so following a "Slično ovome" card from one product to another
    // changes only the parameter — with a snapshot read in the constructor the URL changed and the
    // page kept showing the previous product, which is the "clicking a product doesn't load it" bug.
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      const id = params.get('id');
      if (!id) {
        this.loadError.set('Proizvod nije pronađen.');
        this.isLoading.set(false);
        return;
      }
      this.load(id);
    });
  }

  /** Everything the previous product left behind, cleared before the next one's requests land —
   * otherwise a reused component carries over its gallery index, cart quantities and held spot. */
  private resetForNewProduct(): void {
    this.product.set(null);
    this.sectors.set([]);
    this.organization.set(null);
    this.similarProducts.set([]);
    this.activeImageIndex.set(0);
    this.isDescriptionExpanded.set(false);
    this.quantities.set({});
    this.selectedDate.set(this.tomorrowIso());
    this.selectedSpotSectorId.set(null);
    this.selectedSpotHoldId = null;
    this.takenSpotIds.set(new Set());
    this.submitError.set(null);
    this.loadError.set(null);
    this.isSubmitting.set(false);
    this.isLoading.set(true);
    this.isLoadingSimilar.set(true);
  }

  private load(id: string): void {
    this.resetForNewProduct();

    forkJoin({
      product: this.catalogService.getProductById(id),
      // No date on this first request: the mode isn't known until the product lands, and for
      // DailyEntry the initial date is itself derived from the sectors that come back. The
      // follow-up request in loadSectorsForDate() is what fills availability in for that mode.
      sectors: this.sectorService.getSectors(id),
    }).subscribe({
      next: ({ product, sectors }) => {
        this.product.set(product);
        this.sectors.set(sectors.items);
        this.isLoading.set(false);
        if (product.ticketingMode === 'DailyEntry') {
          this.selectedDate.set(this.firstSelectableDate(sectors.items));
          this.loadSectorsForDate();
        }
        this.loadOrganization(product.organizationId);
        this.trackView(product.id);
        this.loadSimilar(product.id);
      },
      error: () => {
        this.loadError.set('Proizvod nije pronađen ili više nije dostupan.');
        this.isLoading.set(false);
      },
    });
  }

  /** Re-reads the sector list for the currently-selected date so `remainingCapacity` refers to that
   * specific day. DailyEntry only — every other mode's availability is date-independent and was
   * already answered by the initial request. */
  private loadSectorsForDate(): void {
    const product = this.product();
    if (!product || product.ticketingMode !== 'DailyEntry') return;

    this.isReloadingAvailability.set(true);
    this.sectorService
      .getSectors(product.id, this.selectedDate())
      .pipe(catchError(() => of(null)))
      .subscribe((result) => {
        // A failed refresh keeps whatever was on screen rather than blanking the card — the buyer
        // can still try the hold, and the backend is the authority on capacity regardless.
        if (result) this.sectors.set(result.items);
        this.isReloadingAvailability.set(false);
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

  /**
   * Records that this product was opened, feeding the recommender. Signed-in visitors only —
   * there is no anonymous identity to attribute a view to, and the endpoint requires auth.
   *
   * Browser-only (SSR would double-count every page render) and error-swallowing: a failed view
   * write must never surface to someone who is just browsing. It still has to be subscribed,
   * though — an unsubscribed HttpClient observable never issues the request at all.
   */
  private trackView(productId: string): void {
    if (!this.isBrowser || !this.authService.isAuthenticated()) return;

    this.recommendationService
      .trackView(productId)
      .pipe(catchError(() => of(void 0)))
      .subscribe();
  }

  /** "Slično ovome" — public, so it loads for anonymous visitors too. Same treatment as
   * loadOrganization: its own request, swallowing its own error, never blocking the page. */
  private loadSimilar(productId: string): void {
    this.isLoadingSimilar.set(true);
    this.recommendationService
      .getSimilar(productId, 3)
      .pipe(catchError(() => of([] as Product[])))
      .subscribe((products) => {
        this.similarProducts.set(products);
        this.isLoadingSimilar.set(false);
      });
  }

  private tomorrowIso(): string {
    return toIsoDate(tomorrow());
  }

  /** Loads the Google Maps JS API script exactly once per page (shared across every
   * product-details view a buyer navigates to in one session), then flips mapsReady so the
   * @if in the template renders <google-map>. Dynamic <script> injection, rather than a static
   * tag in index.html, because the API key comes from the repo-root `.env` through environment.ts
   * (baked in at build time by @ngx-env/builder) and index.html has no template step to read it
   * from. Only ever called when a key is actually configured — see hasMapsKey. */
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

  toggleDescription(): void {
    this.isDescriptionExpanded.update((expanded) => !expanded);
  }

  quantityFor(row: PurchaseRow): number {
    return this.quantities()[rowKey(row.sectorId, row.ticketTypeId)] ?? 0;
  }

  /** Total already picked across every row of one sector. Named ticket types draw on one shared
   * capacity pool, so the ceiling has to be checked per sector, not per row — otherwise three
   * tiers of a 5-seat sector would each happily go to 5 and the hold would fail at checkout. */
  private selectedInSector(sectorId: string): number {
    const quantities = this.quantities();
    return this.rows()
      .filter((row) => row.sectorId === sectorId)
      .reduce((sum, row) => sum + (quantities[rowKey(row.sectorId, row.ticketTypeId)] ?? 0), 0);
  }

  /** Whether "+" should still be live for this row. Null remaining means the backend couldn't say,
   * so only the storefront cap applies — an unknown must not lock the stepper. */
  canIncrement(row: PurchaseRow): boolean {
    if (this.quantityFor(row) >= MAX_PER_ORDER) return false;
    if (row.remaining === null) return true;
    return this.selectedInSector(row.sectorId) < row.remaining;
  }

  incQuantity(row: PurchaseRow): void {
    if (!this.canIncrement(row)) return;
    const key = rowKey(row.sectorId, row.ticketTypeId);
    this.quantities.update((q) => ({ ...q, [key]: (q[key] ?? 0) + 1 }));
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
    // Availability is per (sector, date) in this mode, so even a same-month change needs a refresh.
    this.loadSectorsForDate();
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
        // Someone else took the capacity between page load and this click, so what is on screen is
        // now stale in exactly the way that matters. Re-read it rather than leaving a "5 dostupno"
        // label standing next to the message saying there weren't 5.
        if (this.isConflict(failed.error)) this.refreshAvailability();
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

  /** Re-reads the live counters for whatever this product's mode keys on. */
  private refreshAvailability(): void {
    const product = this.product();
    if (!product) return;

    if (product.ticketingMode === 'DailyEntry') {
      this.loadSectorsForDate();
      return;
    }

    this.sectorService
      .getSectors(product.id)
      .pipe(catchError(() => of(null)))
      .subscribe((result) => {
        if (result) this.sectors.set(result.items);
      });
  }

  /** RecurringReservation — clicking a space attempts a live 1-quantity hold
   * immediately (D3: no fabricated spatial layout, taken/free is a real
   * hold attempt against the same mechanism every purchase path uses). */
  selectSpot(sector: Sector): void {
    if (this.isSubmitting() || this.spotUnavailable(sector)) return;

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
