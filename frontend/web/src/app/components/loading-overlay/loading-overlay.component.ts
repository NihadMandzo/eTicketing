import { ChangeDetectionStrategy, Component, inject } from '@angular/core';

import { LoadingService } from '../../core/services/loading.service';

/**
 * The global blocking overlay: while any backend request is in flight, the page is covered and
 * cannot be interacted with.
 *
 * Two details are deliberate and worth keeping:
 *
 * 1. **It blocks immediately, but only becomes visible after a beat.** The element is in the DOM
 *    the instant a request starts, so a double-click or a second submit can never get through —
 *    that is the "freeze" half. Its *appearance* is delayed by a CSS animation (see the stylesheet),
 *    because most requests here finish in well under 100ms and flashing a grey scrim on every one
 *    of them looks like a fault rather than like progress.
 * 2. **It lives in `App`, above the router outlet**, not in `LayoutComponent` — so it also covers
 *    the pages that render outside the layout shell, and it is never torn down and rebuilt by a
 *    navigation.
 *
 * The boot phase (before Angular has hydrated at all) is covered by a separate, plain-HTML splash
 * in `index.html`, which this component knows nothing about — by the time this exists, that one has
 * already removed itself.
 */
@Component({
  selector: 'app-loading-overlay',
  templateUrl: './loading-overlay.component.html',
  styleUrl: './loading-overlay.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoadingOverlayComponent {
  private readonly loadingService = inject(LoadingService);

  readonly isLoading = this.loadingService.isLoading;
}
