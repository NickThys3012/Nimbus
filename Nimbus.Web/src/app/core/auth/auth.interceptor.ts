import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { AuthStore } from './auth.store';

// These answer 401/403 as part of normal operation (wrong password, "is anyone signed in?"
// probe on startup), so they must not bounce the user to the login screen.
const AUTH_ENDPOINTS = [
  '/api/Authentication/login',
  '/api/Authentication/logout',
  '/api/Authentication/me',
];

/**
 * Handles the two ways the API says "no" to a signed-in session. The session is an HttpOnly cookie the
 * browser attaches by itself, so there is nothing to add to requests here — only responses to react to:
 *
 *  - 401: the session ended (expired, or the account was blocked/deleted/changed). Forget the user, give
 *    forms a chance to stash unsaved work, and go to login with the current URL as `returnUrl`.
 *  - 403: signed in but not allowed. Show the access-denied screen (pending/rejected accounts get 403s
 *    by design and already have their own notice in the shell, so they are left alone).
 *
 * Register with `provideHttpClient(withInterceptors([xsrfInterceptor, authInterceptor]))`.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authStore = inject(AuthStore);
  const router = inject(Router);
  const isAuthEndpoint = AUTH_ENDPOINTS.some((endpoint) => req.url.includes(endpoint));

  return next(req).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || isAuthEndpoint) {
        return throwError(() => error);
      }

      if (error.status === 401) {
        const url = router.url;
        // Already on a public auth screen: nothing to return to, and re-routing would loop.
        if (!url.startsWith('/login') && !url.startsWith('/access-denied')) {
          authStore.expireSession(url);
          void router.navigate(['/login'], { queryParams: { returnUrl: url } });
        }
      } else if (error.status === 403) {
        if (!authStore.isPendingApproval() && !authStore.isAccessRejected()) {
          void router.navigate(['/access-denied']);
        }
      }

      return throwError(() => error);
    }),
  );
};
