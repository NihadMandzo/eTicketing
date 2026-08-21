import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withInMemoryScrolling } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { routes } from './app.routes';
import { provideClientHydration, withEventReplay } from '@angular/platform-browser';
import { authRefreshInterceptor } from './core/interceptors/auth-refresh.interceptor';
import { credentialsInterceptor } from './core/interceptors/credentials.interceptor';
import { AuthService } from './core/services/auth.service';
import { ThemeService } from './core/services/theme.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Every navigation lands at the top of the new screen. Without this the
    // router leaves the scroll offset untouched, so following a footer link
    // from halfway down a long list opened the next page mid-way down. Back
    // and forward still restore where the user actually was.
    provideRouter(routes, withInMemoryScrolling({ scrollPositionRestoration: 'enabled' })),
    provideClientHydration(withEventReplay()),
    provideHttpClient(withFetch(), withInterceptors([credentialsInterceptor, authRefreshInterceptor])),
    // Restores auth state from the session cookie on every page load/refresh,
    // so a logged-in user doesn't appear signed out after an F5.
    provideAppInitializer(() => firstValueFrom(inject(AuthService).loadCurrentUser())),
    // Applies the persisted/system light-dark theme before first paint.
    provideAppInitializer(() => inject(ThemeService).init()),
  ]
};
