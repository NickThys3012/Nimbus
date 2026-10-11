import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';

import { AntiforgeryService } from '../api-client';
import { xsrfInterceptor } from './xsrf.interceptor';

describe('xsrfInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  const getApiAntiforgery = vi.fn();

  const setCookie = (value: string | null) => {
    document.cookie =
      value === null
        ? 'XSRF-TOKEN=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/'
        : `XSRF-TOKEN=${value}; path=/`;
  };

  beforeEach(() => {
    setCookie(null);
    getApiAntiforgery.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([xsrfInterceptor])),
        provideHttpClientTesting(),
        { provide: AntiforgeryService, useValue: { getApiAntiforgery } },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    setCookie(null);
  });

  it('adds the token header to state-changing API requests', () => {
    setCookie('abc123');
    http.post('/api/Authentication/logout', {}).subscribe();
    const req = httpMock.expectOne('/api/Authentication/logout');
    expect(req.request.headers.get('X-XSRF-TOKEN')).toBe('abc123');
    req.flush({});
  });

  it.each(['put', 'patch', 'delete'] as const)('covers %s', (method) => {
    setCookie('abc123');
    http.request(method, '/api/admin/users/1').subscribe();
    const req = httpMock.expectOne('/api/admin/users/1');
    expect(req.request.headers.get('X-XSRF-TOKEN')).toBe('abc123');
    req.flush({});
  });

  it('leaves safe methods alone', () => {
    setCookie('abc123');
    http.get('/api/admin/users').subscribe();
    const req = httpMock.expectOne('/api/admin/users');
    expect(req.request.headers.has('X-XSRF-TOKEN')).toBe(false);
    req.flush([]);
  });

  it('never sends the token to a non-API host', () => {
    setCookie('abc123');
    http.post('https://elsewhere.example/collect', {}).subscribe();
    const req = httpMock.expectOne('https://elsewhere.example/collect');
    expect(req.request.headers.has('X-XSRF-TOKEN')).toBe(false);
    req.flush({});
  });

  it('fetches the token first when the cookie is missing', () => {
    getApiAntiforgery.mockImplementation(() => {
      setCookie('fresh');
      return of(undefined);
    });
    http.post('/api/Authentication/login', {}).subscribe();
    const req = httpMock.expectOne('/api/Authentication/login');
    expect(getApiAntiforgery).toHaveBeenCalledTimes(1);
    expect(req.request.headers.get('X-XSRF-TOKEN')).toBe('fresh');
    req.flush({});
  });
});
