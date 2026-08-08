import { DOCUMENT } from '@angular/common';
import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

const STORAGE_KEY = 'eticketing-theme';

export type Theme = 'light' | 'dark';

/**
 * Applies/persists the light/dark theme by toggling a `dark` class on
 * `<html>`, matching the `.dark { ... }` variable block in styles.css.
 * The user's choice is stored in this browser's localStorage only — each
 * platform (web/desktop/mobile) keeps its own independent preference.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly theme = signal<Theme>('light');

  /** Reads the persisted/system preference and applies it. Call once on app init. */
  init(): void {
    if (!this.isBrowser) return;

    const stored = window.localStorage.getItem(STORAGE_KEY) as Theme | null;
    const initial = stored ?? (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
    this.apply(initial);
  }

  toggle(): void {
    this.apply(this.theme() === 'dark' ? 'light' : 'dark');
  }

  private apply(theme: Theme): void {
    this.theme.set(theme);
    this.document.documentElement.classList.toggle('dark', theme === 'dark');
    if (this.isBrowser) {
      window.localStorage.setItem(STORAGE_KEY, theme);
    }
  }
}
