import { ApplicationConfig, ErrorHandler } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs';

import { routes } from './Nimbus.routes';
import { AngularHttpRequest } from './core/api-client/core/AngularHttpRequest';
import { BaseHttpRequest } from './core/api-client/core/BaseHttpRequest';
import { OpenAPI } from './core/api-client';
import { TelemetryErrorHandler, TelemetryService } from './core/telemetry/telemetry.service';

// Base URL for the generated API client (see `npm run generate:api`).
// Points at the local dev API by default.
OpenAPI.BASE = 'http://localhost:5214';

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
    .subscribe((event) => telemetry.reportPageView(event.urlAfterRedirects));
}

