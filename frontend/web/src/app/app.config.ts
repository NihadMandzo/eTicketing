import {
  provideHttpClient,
  withFetch,
  withInterceptors,
  withInterceptorsFromDi,
} from '@angular/common/http';
import {
  ApplicationConfig,
  PLATFORM_ID,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { routes } from './app.routes';
import { provideClientHydration, withEventReplay } from '@angular/platform-browser';
import { authRefreshInterceptor } from './core/interceptors/auth-refresh.interceptor';
import { credentialsInterceptor } from './core/interceptors/credentials.interceptor';
import { loadingInterceptor } from './core/interceptors/loading.interceptor';
import { AuthService } from './core/services/auth.service';
import { ThemeService } from './core/services/theme.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // `'top'`, not `'enabled'`. Both send an ordinary link click to the top of the next screen, but
    // `'enabled'` additionally replays a remembered scroll offset on back/forward — and every page
    // here renders from an HTTP response that lands *after* the navigation completes. So at the
    // moment it restored, the document was still a spinner one viewport tall, the offset got clamped
    // to whatever fitted, and going back landed at an arbitrary point in the page. `'top'` drops the
    // replay: coming back simply loads that page again, from the top.
    provideRouter(
      routes,
      withInMemoryScrolling({ scrollPositionRestoration: 'top', anchorScrolling: 'enabled' }),
      // Lets the error-page routes (error.routes.ts) supply their 401/403/404/500 copy through
      // plain route `data` bound straight onto ErrorPageComponent's inputs, instead of each variant
      // needing its own thin wrapper component.
      withComponentInputBinding(),
    ),
    provideClientHydration(withEventReplay()),
    // Order matters: loadingInterceptor must see every request, including ones the refresh
    // interceptor retries after a 401 — placing it first means the overlay stays up across that
    // retry instead of flickering off and back on between the failed call and its replay.
    // withInterceptorsFromDi alongside the functional list: it is what lets app.config.server.ts
    // add SsrApiBaseInterceptor for server rendering only, without calling provideHttpClient a
    // second time and clobbering this configuration when the two configs are merged.
    provideHttpClient(
      withFetch(),
      withInterceptors([loadingInterceptor, credentialsInterceptor, authRefreshInterceptor]),
      withInterceptorsFromDi(),
    ),
    // Restores auth state from the session cookie on every page load/refresh, so a logged-in user
    // doesn't appear signed out after an F5.
    //
    // Browser-only. On the server there is no session cookie to read — prerendering happens at
    // build time with no backend at all, and the per-request SSR path doesn't forward the visitor's
    // cookies to HttpClient — so the call could only ever fail there, while still costing a request
    // per render and risking a "signed out" answer being carried into the client through hydration's
    // HTTP transfer cache.
    provideAppInitializer(() => {
      if (!isPlatformBrowser(inject(PLATFORM_ID))) return;
      return firstValueFrom(inject(AuthService).loadCurrentUser());
    }),
    // Applies the persisted/system light-dark theme before first paint.
    provideAppInitializer(() => inject(ThemeService).init()),
  ],
};
