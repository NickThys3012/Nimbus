import { inject } from '@angular/core';
import {
  HttpErrorResponse,
  HttpInterceptorFn,
} from '@angular/common/http';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';

import { AuthStore } from './auth.store';

// Requests to these endpoints must go out untouched: attaching a (possibly
// stale) access token to `/refresh` would be pointless, and retrying a
// failed `/login` or `/refresh` call would risk an infinite loop.
const AUTH_ENDPOINTS = ['/api/Authentication/login', '/api/Authentication/refresh'];

/**
 * Reading one flight (the edit form's load and the read-only view's load). A 403 there means "this
 * flight exists but is not yours to open", which those pages answer themselves by going to the
 * flight list, so the global access-denied screen must not jump in first.
 */
const FLIGHT_READ_URL = /^\/api\/flight-preparations\/[0-9a-f-]{36}(\/detail)?$/i;

/**
 * Attaches the in-memory access token to outgoing API requests and
 * transparently retries once on a 401 by attempting a silent refresh
 * (via the httpOnly refresh-token cookie). If the session cannot be restored
 * the user is routed to login with the current URL as `returnUrl`; a 403 routes
 * to the access-denied screen. Register with
 * `provideHttpClient(withInterceptors([authInterceptor]))`.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authStore = inject(AuthStore);
  const router = inject(Router);
  const isAuthEndpoint = AUTH_ENDPOINTS.some((endpoint) => req.url.includes(endpoint));

  const token = authStore.accessToken();
  const authedReq = token && !isAuthEndpoint
    ? req.clone({ setHeaders: { Authorization: 'Bearer ' + token } })
    : req;

  const toLogin = () => {
    const url = router.url;
    // Already on a public auth screen: nothing to return to, and re-routing would loop.
    if (url.startsWith('/login') || url.startsWith('/access-denied')) {
      return;
    }
    void router.navigate(['/login'], { queryParams: { returnUrl: url } });
  };

  return next(authedReq).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || isAuthEndpoint) {
        return throwError(() => error);
      }

      if (error.status === 403) {
        // Pending/rejected accounts get 403s by design; the shell already explains that state.
        const pageHandlesIt = req.method === 'GET' && FLIGHT_READ_URL.test(req.url.split('?')[0]);
        if (!pageHandlesIt && !authStore.isPendingApproval() && !authStore.isAccessRejected()) {
          void router.navigate(['/access-denied']);
        }
        return throwError(() => error);
      }

      if (error.status !== 401) {
        return throwError(() => error);
      }

      return authStore.restoreSession().pipe(
        switchMap((restored) => {
          if (!restored) {
            toLogin();
            return throwError(() => error);
          }

          const retriedReq = req.clone({
            setHeaders: { Authorization: `Bearer ${authStore.accessToken()!}` },
          });
          return next(retriedReq);
        }),
      );
    }),
  );
};
