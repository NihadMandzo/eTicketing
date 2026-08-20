import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

/**
 * A reusable pagination bar, with an optional page-size selector — mirrors
 * `frontend/desktop`'s `PaginationBar` widget contract exactly (same
 * 0-indexed `currentPage`, same ellipsis-collapsing page-number algorithm,
 * same default page-size options) so behavior is consistent across clients.
 *
 * `currentPage` is 0-indexed, matching the locked `PagedResult<T>`
 * pagination contract (see 01-domain.md) — `Page: 0` is the first page.
 */
@Component({
  selector: 'app-pagination-bar',
  standalone: true,
  templateUrl: './pagination-bar.component.html',
  styleUrl: './pagination-bar.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PaginationBarComponent {
  readonly currentPage = input.required<number>();
  readonly totalPages = input.required<number>();
  readonly pageSize = input<number | null>(null);
  readonly pageSizeOptions = input<number[]>([5, 10, 20, 50]);

  readonly pageChanged = output<number>();
  readonly pageSizeChanged = output<number>();

  readonly showPageSize = computed(() => this.pageSize() !== null);

  readonly pageNumbers = computed<(number | null)[]>(() => {
    const total = this.totalPages();
    const current = this.currentPage();
    if (total <= 7) return Array.from({ length: total }, (_, i) => i);

    const pages: (number | null)[] = [0];
    if (current > 2) pages.push(null);
    const start = Math.min(Math.max(current - 1, 1), total - 2);
    const end = Math.min(Math.max(current + 1, 1), total - 2);
    for (let i = start; i <= end; i++) pages.push(i);
    if (current < total - 3) pages.push(null);
    pages.push(total - 1);
    return pages;
  });

  goToPage(page: number): void {
    if (page < 0 || page >= this.totalPages() || page === this.currentPage()) return;
    this.pageChanged.emit(page);
  }

  onPageSizeChange(value: string): void {
    this.pageSizeChanged.emit(Number(value));
  }
}
