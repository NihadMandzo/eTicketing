import { HTTP_INTERCEPTORS } from '@angular/common/http';
import { mergeApplicationConfig, ApplicationConfig } from '@angular/core';
import { provideServerRendering, withRoutes } from '@angular/ssr';
import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';
import { SsrApiBaseInterceptor } from './core/interceptors/ssr-api-base.interceptor';

const serverConfig: ApplicationConfig = {
  providers: [
    provideServerRendering(withRoutes(serverRoutes)),
    // Server-only: the app's API base URL is relative so the browser stays same-origin, and a
    // relative URL cannot be resolved by a Node process. See the interceptor for the full why.
    { provide: HTTP_INTERCEPTORS, useClass: SsrApiBaseInterceptor, multi: true },
  ],
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
