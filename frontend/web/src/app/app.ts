import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { Component, PLATFORM_ID, afterNextRender, inject, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { LoadingOverlayComponent } from './components/loading-overlay/loading-overlay.component';

/** Matches the fade in index.html's `#app-boot-splash` transition. */
const SPLASH_FADE_MS = 250;

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, LoadingOverlayComponent],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  private readonly document = inject(DOCUMENT);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  protected readonly title = signal('web');

  constructor() {
    // Dismisses the boot splash from index.html. `afterNextRender` rather than the constructor
    // body: it runs browser-only and only once the first view has actually been painted, so the
    // splash lifts onto real content rather than onto an empty frame.
    //
    // By this point Angular has bootstrapped, which means every `provideAppInitializer` has already
    // resolved — including the session restore — so the app underneath is genuinely ready, not just
    // mounted.
    afterNextRender(() => this.dismissBootSplash());
  }

  private dismissBootSplash(): void {
    if (!this.isBrowser) return;

    const splash = this.document.getElementById('app-boot-splash');
    if (!splash) return;

    splash.classList.add('is-hidden');
    // Removed rather than left invisible: it is a fixed, full-viewport element, and leaving it in
    // the tree keeps an inert layer over the page for every assistive technology that ignores
    // `pointer-events`.
    this.document.defaultView?.setTimeout(() => splash.remove(), SPLASH_FADE_MS);
  }
}
