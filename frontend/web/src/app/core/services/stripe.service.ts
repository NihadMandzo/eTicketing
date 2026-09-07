import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject } from '@angular/core';
import type { Appearance, Stripe, StripeElements } from '@stripe/stripe-js';
import { ThemeService } from './theme.service';

/**
 * Loads Stripe.js and builds the Elements appearance from this app's own CSS custom properties.
 *
 * Everything here is guarded by `isPlatformBrowser`: SSR is real on this app (see
 * .claude/rules/20-frontend-web.md) and `loadStripe` touches `document`, so it would break server
 * rendering. The dynamic `import()` means the module is not even pulled in on the server — the same
 * shape `product-details.component.ts` already uses for the Google Maps script.
 */
@Injectable({ providedIn: 'root' })
export class StripeService {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly themeService = inject(ThemeService);

  /** One Stripe instance per publishable key, so switching keys in dev doesn't leak instances. */
  private readonly loaded = new Map<string, Promise<Stripe | null>>();

  load(publishableKey: string): Promise<Stripe | null> {
    if (!this.isBrowser) return Promise.resolve(null);

    const cached = this.loaded.get(publishableKey);
    if (cached) return cached;

    const loading = import('@stripe/stripe-js').then((m) => m.loadStripe(publishableKey));
    this.loaded.set(publishableKey, loading);
    return loading;
  }

  createElements(stripe: Stripe, clientSecret: string): StripeElements {
    return stripe.elements({ clientSecret, appearance: this.appearance() });
  }

  /**
   * Re-themes a mounted Element. Must be called when the theme toggles, or the card field keeps
   * its light palette on a dark page — Stripe renders inside an iframe and never sees our CSS.
   */
  retheme(elements: StripeElements): void {
    elements.update({ appearance: this.appearance() });
  }

  /**
   * Maps this app's CSS custom properties (styles.css `:root` / `.dark`) onto Stripe's appearance
   * API, read live from the document so it always reflects the active theme.
   */
  private appearance(): Appearance {
    const css = (name: string, fallback: string): string => {
      if (!this.isBrowser) return fallback;
      const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
      return value || fallback;
    };

    return {
      theme: this.themeService.theme() === 'dark' ? 'night' : 'stripe',
      variables: {
        colorPrimary: css('--primary', '#1D5B3A'),
        colorBackground: css('--input-background', '#ffffff'),
        colorText: css('--foreground', '#111111'),
        colorTextSecondary: css('--muted-foreground', '#6b7280'),
        colorDanger: css('--destructive', '#DC2626'),
        borderRadius: css('--radius', '8px'),
        fontFamily: 'inherit',
      },
    };
  }
}
