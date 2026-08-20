import { ComponentRef } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PaginationBarComponent } from './pagination-bar.component';

describe('PaginationBarComponent', () => {
  let fixture: ComponentFixture<PaginationBarComponent>;
  let ref: ComponentRef<PaginationBarComponent>;

  async function render(inputs: { currentPage: number; totalPages: number; pageSize?: number | null }) {
    fixture = TestBed.createComponent(PaginationBarComponent);
    ref = fixture.componentRef;
    ref.setInput('currentPage', inputs.currentPage);
    ref.setInput('totalPages', inputs.totalPages);
    ref.setInput('pageSize', inputs.pageSize ?? null);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  function select(el: HTMLElement): HTMLSelectElement | null {
    return el.querySelector('#page-size-select');
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [PaginationBarComponent] }).compileComponents();
  });

  describe('page-size selector', () => {
    // The regression: `[value]` on the <select> was applied before @for had
    // created the <option>s, so the browser fell back to the first option and
    // the control read 5 while the list was actually paged by 20.
    it('shows the current page size, not the first option', async () => {
      const el = await render({ currentPage: 0, totalPages: 3, pageSize: 20 });

      expect(select(el)!.value).toBe('20');
    });

    it('follows the page size when it changes', async () => {
      const el = await render({ currentPage: 0, totalPages: 3, pageSize: 20 });
      ref.setInput('pageSize', 50);
      await fixture.whenStable();

      expect(select(el)!.value).toBe('50');
    });

    it('marks exactly one option selected', async () => {
      const el = await render({ currentPage: 0, totalPages: 3, pageSize: 10 });
      const options = Array.from(el.querySelectorAll('option'));

      expect(options.filter((o) => o.selected).map((o) => o.value)).toEqual(['10']);
    });

    // Gating the whole bar on `totalPages() > 1` meant choosing a size big
    // enough to fit every row on one page removed the only control that could
    // change it back.
    it('stays reachable when everything fits on a single page', async () => {
      const el = await render({ currentPage: 0, totalPages: 1, pageSize: 50 });

      expect(select(el)).not.toBeNull();
      expect(el.querySelector('.pager')).toBeNull();
    });

    it('emits the chosen size as a number', async () => {
      const el = await render({ currentPage: 0, totalPages: 3, pageSize: 20 });
      const emitted: number[] = [];
      fixture.componentInstance.pageSizeChanged.subscribe((size) => emitted.push(size));

      const dropdown = select(el)!;
      dropdown.value = '5';
      dropdown.dispatchEvent(new Event('change'));

      expect(emitted).toEqual([5]);
    });

    it('is omitted entirely when no page size is supplied', async () => {
      const el = await render({ currentPage: 0, totalPages: 4, pageSize: null });

      expect(select(el)).toBeNull();
      expect(el.querySelector('.pager')).not.toBeNull();
    });
  });

  describe('pager', () => {
    it('renders nothing at all for a single page with no size selector', async () => {
      const el = await render({ currentPage: 0, totalPages: 1, pageSize: null });

      expect(el.querySelector('.pagination-bar')).toBeNull();
    });

    it('labels pages 1-based while paging 0-indexed', async () => {
      const el = await render({ currentPage: 0, totalPages: 3, pageSize: null });
      const labels = Array.from(el.querySelectorAll('.page-btn')).map((b) => b.textContent?.trim());

      expect(labels).toEqual(['1', '2', '3']);
      expect(el.querySelector('.page-btn.active')?.textContent?.trim()).toBe('1');
    });

    it('disables the arrows at each end', async () => {
      const el = await render({ currentPage: 0, totalPages: 3, pageSize: null });
      const [prev, next] = Array.from(el.querySelectorAll<HTMLButtonElement>('.nav-btn'));
      expect(prev.disabled).toBe(true);
      expect(next.disabled).toBe(false);

      ref.setInput('currentPage', 2);
      await fixture.whenStable();
      const [prevLast, nextLast] = Array.from(el.querySelectorAll<HTMLButtonElement>('.nav-btn'));
      expect(prevLast.disabled).toBe(false);
      expect(nextLast.disabled).toBe(true);
    });

    it('collapses long ranges with an ellipsis but always keeps first and last', async () => {
      const el = await render({ currentPage: 5, totalPages: 12, pageSize: null });
      const labels = Array.from(el.querySelectorAll('.page-btn')).map((b) => b.textContent?.trim());

      expect(labels[0]).toBe('1');
      expect(labels[labels.length - 1]).toBe('12');
      expect(el.querySelectorAll('.ellipsis').length).toBe(2);
    });

    it('ignores a click on the page already shown', async () => {
      const el = await render({ currentPage: 1, totalPages: 3, pageSize: null });
      const emitted: number[] = [];
      fixture.componentInstance.pageChanged.subscribe((page) => emitted.push(page));

      el.querySelector<HTMLButtonElement>('.page-btn.active')!.click();

      expect(emitted).toEqual([]);
    });
  });
});
