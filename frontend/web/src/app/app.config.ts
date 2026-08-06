import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { routes } from './app.routes';
import { provideClientHydration, withEventReplay } from '@angular/platform-browser';
import { authRefreshInterceptor } from './core/interceptors/auth-refresh.interceptor';
import { credentialsInterceptor } from './core/interceptors/credentials.interceptor';
import { AuthService } from './core/services/auth.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideClientHydration(withEventReplay()),
    provideHttpClient(withFetch(), withInterceptors([credentialsInterceptor, authRefreshInterceptor])),
    // Restores auth state from the session cookie on every page load/refresh,
    // so a logged-in user doesn't appear signed out after an F5.
    provideAppInitializer(() => firstValueFrom(inject(AuthService).loadCurrentUser())),
  ]
};
