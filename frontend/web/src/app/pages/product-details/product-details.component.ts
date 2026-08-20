import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';

import { CatalogService } from '../../core/services/catalog.service';
import { SectorService } from '../../core/services/sector.service';
import { CartHoldGroup, CartService } from '../../core/services/cart.service';
import { Product } from '../../core/models/catalog.models';
import { Sector } from '../../core/models/sector.models';

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

@Component({
  selector: 'app-product-details',
  standalone: true,
  imports: [RouterLink, DatePipe],
  templateUrl: './product-details.component.html',
  styleUrl: './product-details.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductDetailsComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly catalogService = inject(CatalogService);
  private readonly sectorService = inject(SectorService);
  private readonly cartService = inject(CartService);

  readonly product = signal<Product | null>(null);
  readonly sectors = signal<Sector[]>([]);
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

  readonly isSubmitting = signal(false);
  readonly submitError = signal<string | null>(null);

  readonly minDate = this.tomorrowIso();

  readonly rows = computed<PurchaseRow[]>(() =>
    this.sectors().flatMap((sector): PurchaseRow[] =>
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

  constructor() {
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
        this.isLoading.set(false);
      },
      error: () => {
        this.loadError.set('Proizvod nije pronađen ili više nije dostupan.');
        this.isLoading.set(false);
      },
    });
  }

  private tomorrowIso(): string {
    const d = new Date();
    d.setDate(d.getDate() + 1);
    return d.toISOString().slice(0, 10);
  }

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
    this.selectedDate.set(value);
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

    const holdCalls = Array.from(bySector.entries()).map(([sectorId, sectorRows]) => {
      const quantity = sectorRows.reduce((sum, row) => sum + this.quantityFor(row), 0);
      return this.sectorService
        .hold(sectorId, { quantity, date: isDailyEntry ? this.selectedDate() : null })
        .pipe();
    });

    forkJoin(holdCalls).subscribe({
      next: (holdResponses) => {
        const holds: CartHoldGroup[] = Array.from(bySector.entries()).map(([sectorId, sectorRows], i) => ({
          holdId: holdResponses[i].holdId,
          sectorId,
          sectorName: sectorRows[0].sectorName,
          lineItems: sectorRows.map((row) => ({
            ticketTypeId: row.ticketTypeId,
            ticketTypeName: row.ticketTypeName,
            quantity: this.quantityFor(row),
            unitPrice: row.price,
          })),
          total: sectorRows.reduce((sum, row) => sum + this.quantityFor(row) * row.price, 0),
        }));

        this.cartService.set({
          productId: product.id,
          productName: product.name,
          date: isDailyEntry ? this.selectedDate() : null,
          holds,
        });
        this.isSubmitting.set(false);
        this.router.navigateByUrl('/placanje');
      },
      error: (error: unknown) => {
        this.isSubmitting.set(false);
        if (this.isUnauthorized(error)) {
          this.router.navigateByUrl('/prijava');
          return;
        }
        this.submitError.set(this.extractErrorMessage(error));
      },
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
              total: sector.price,
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
