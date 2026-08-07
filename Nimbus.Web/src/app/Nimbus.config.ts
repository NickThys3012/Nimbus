import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';

import { routes } from './Nimbus.routes';
import { AngularHttpRequest } from './core/api-client/core/AngularHttpRequest';
import { BaseHttpRequest } from './core/api-client/core/BaseHttpRequest';
import { OpenAPI } from './core/api-client';

// Base URL for the generated API client (see `npm run generate:api`).
// Points at the local dev API by default.
OpenAPI.BASE = 'http://localhost:5214';

export const nimbusConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(),
    // Generated services (e.g. AuthenticationService) depend on
    // `BaseHttpRequest`, which in turn depends on the `OpenAPI` config
    // object as an injection token. The generated `NimbusApiClient`
    // NgModule provides both, but standalone apps don't import NgModules
    // by default, so they must be provided explicitly here.
    { provide: OpenAPI, useValue: OpenAPI },
    { provide: BaseHttpRequest, useClass: AngularHttpRequest },
  ]
};
