import { ApplicationConfig, ErrorHandler, isDevMode } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs';

import { routes } from './Nimbus.routes';
import { AngularHttpRequest } from './core/api-client/core/AngularHttpRequest';
import { BaseHttpRequest } from './core/api-client/core/BaseHttpRequest';
import { OpenAPI } from './core/api-client';
import { TelemetryErrorHandler, TelemetryService } from './core/telemetry/telemetry.service';

// Base URL for the generated API client (see `npm run generate:api`).
// In production the SPA is served by the same API process (see the
// Dockerfile: the Angular build output is copied into wwwroot/), so requests
// should go to the same origin. Only point at the separately-running local
// dev API server when running under `ng serve`.
OpenAPI.BASE = isDevMode() ? 'http://localhost:5214' : '';

export const nimbusConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(),
    // Generated services (e.g. AuthenticationService) depend on
    // `BaseHttpRequest`, which in turn depends on the `OpenAPI` config
    // object as an injection token. The generated `NimbusApiClient`
    // NgModule provides both, but standalone apps don't import NgModules
    // by default, so they must be provided explicitly here.
    { provide: OpenAPI, useValue: OpenAPI },
    { provide: BaseHttpRequest, useClass: AngularHttpRequest },
    // Reports every uncaught error to /api/telemetry (issue #12) instead of
    // (only) the browser console, replacing provideBrowserGlobalErrorListeners.
    { provide: ErrorHandler, useClass: TelemetryErrorHandler },
  ]
};

/**
 * Reports a page view on every completed navigation (issue #12). Called once
 * from `main.ts` after bootstrap so it shares the app's injector/router.
 */
export function reportPageViews(router: Router, telemetry: TelemetryService): void {
  router.events
    .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
    .subscribe((event) => {
      const url = event.urlAfterRedirects.split('?')[0].split('#')[0];
      telemetry.reportPageView(url);
    });
}

