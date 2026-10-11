import { DOCUMENT } from '@angular/common';
import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, map, of, switchMap } from 'rxjs';

import { AntiforgeryService, OpenAPI } from '../api-client';

const COOKIE_NAME = 'XSRF-TOKEN';
const HEADER_NAME = 'X-XSRF-TOKEN';
const SAFE_METHODS = ['GET', 'HEAD', 'OPTIONS', 'TRACE'];

/**
 * Echoes the readable `XSRF-TOKEN` cookie in an `X-XSRF-TOKEN` header on every state-changing request to
 * the API, which is what the API's antiforgery check pairs with its HttpOnly cookie. Angular's built-in
 * XSRF support skips absolute URLs, and the dev server talks to the API through one (`OpenAPI.BASE`),
 * hence this explicit interceptor. If the cookie is missing (first visit, expired), the token is fetched
 * from `/api/Antiforgery` first, so no caller has to think about it.
 */
export const xsrfInterceptor: HttpInterceptorFn = (req, next) => {
  const document = inject(DOCUMENT);
  const antiforgery = inject(AntiforgeryService);

  const isApiCall = req.url.startsWith('/api/') || (!!OpenAPI.BASE && req.url.startsWith(OpenAPI.BASE));
  if (SAFE_METHODS.includes(req.method.toUpperCase()) || !isApiCall) {
    return next(req);
  }

  const withToken = (token: string) => req.clone({ setHeaders: { [HEADER_NAME]: token } });
  const existing = readCookie(document, COOKIE_NAME);
  if (existing) {
    return next(withToken(existing));
  }

  return antiforgery.getApiAntiforgery().pipe(
    catchError(() => of(null)),
    map(() => readCookie(document, COOKIE_NAME)),
    switchMap((token) => next(token ? withToken(token) : req)),
  );
};

function readCookie(document: Document, name: string): string | null {
  for (const part of document.cookie.split(';')) {
    const [key, ...value] = part.trim().split('=');
    if (key === name) {
      return decodeURIComponent(value.join('='));
    }
  }
  return null;
}
