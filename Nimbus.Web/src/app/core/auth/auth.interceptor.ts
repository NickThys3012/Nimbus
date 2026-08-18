import { inject } from '@angular/core';
import {
  HttpErrorResponse,
  HttpInterceptorFn,
} from '@angular/common/http';
import { catchError, switchMap, throwError } from 'rxjs';

import { AuthStore } from './auth.store';

// Requests to these endpoints must go out untouched: attaching a (possibly
// stale) access token to `/refresh` would be pointless, and retrying a
// failed `/login` or `/refresh` call would risk an infinite loop.
const AUTH_ENDPOINTS = ['/api/Authentication/login', '/api/Authentication/refresh'];

/**
 * Attaches the in-memory access token to outgoing API requests and
 * transparently retries once on a 401 by attempting a silent refresh
 * (via the httpOnly refresh-token cookie). Register with
 * `provideHttpClient(withInterceptors([authInterceptor]))`.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authStore = inject(AuthStore);
  const isAuthEndpoint = AUTH_ENDPOINTS.some((endpoint) => req.url.includes(endpoint));

  const token = authStore.accessToken();
  const authedReq = token && !isAuthEndpoint
    ? req.clone({ setHeaders: { Authorization: 'Bearer ' + token } })
    : req;

  return next(authedReq).pipe(
    catchError((error: unknown) => {
      const shouldRetry =
        error instanceof HttpErrorResponse && error.status === 401 && !isAuthEndpoint;

      if (!shouldRetry) {
        return throwError(() => error);
      }

      return authStore.restoreSession().pipe(
        switchMap((restored) => {
          if (!restored) {
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
