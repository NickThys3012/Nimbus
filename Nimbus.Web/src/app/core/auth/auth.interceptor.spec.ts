import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';

import { authInterceptor } from './auth.interceptor';
import { AuthStore } from './auth.store';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let navigate: ReturnType<typeof vi.fn>;
  let restoreSession: ReturnType<typeof vi.fn>;
  let store: Record<string, unknown>;

  beforeEach(() => {
    navigate = vi.fn().mockResolvedValue(true);
    restoreSession = vi.fn();
    store = {
      accessToken: () => 'old-token',
      restoreSession,
      isPendingApproval: () => false,
      isAccessRejected: () => false,
    };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthStore, useValue: store },
        { provide: Router, useValue: { navigate, url: '/flights?x=1' } },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('attaches the bearer token', () => {
    http.get('/api/flights').subscribe();
    const req = httpMock.expectOne('/api/flights');
    expect(req.request.headers.get('Authorization')).toBe('Bearer old-token');
    req.flush({});
  });

  it('expired session: routes to login with the current URL when refresh fails', () => {
    restoreSession.mockReturnValue(of(false));
    const errors: unknown[] = [];
    http.get('/api/flights').subscribe({ error: (e) => errors.push(e) });

    httpMock.expectOne('/api/flights').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { returnUrl: '/flights?x=1' } });
    expect(errors).toHaveLength(1);
  });

  it('expired session: retries once with the fresh token when refresh succeeds', () => {
    restoreSession.mockReturnValue(of(true));
    store['accessToken'] = () => 'new-token';
    let body: unknown;
    http.get('/api/flights').subscribe((b) => (body = b));

    httpMock.expectOne('/api/flights').flush(null, { status: 401, statusText: 'Unauthorized' });
    const retry = httpMock.expectOne('/api/flights');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer new-token');
    retry.flush({ ok: true });

    expect(body).toEqual({ ok: true });
    expect(navigate).not.toHaveBeenCalled();
  });

  it('does not refresh or redirect for a failed login', () => {
    http.post('/api/Authentication/login', {}).subscribe({ error: () => undefined });
    httpMock
      .expectOne('/api/Authentication/login')
      .flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(restoreSession).not.toHaveBeenCalled();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('routes to access-denied on 403', () => {
    http.get('/api/admin').subscribe({ error: () => undefined });
    httpMock.expectOne('/api/admin').flush(null, { status: 403, statusText: 'Forbidden' });
    expect(navigate).toHaveBeenCalledWith(['/access-denied']);
  });

  it('leaves a 403 on reading one flight to the page, which sends the user to the flight list itself', () => {
    const id = '3f2504e0-4f89-41d3-9a0c-0305e82c3301';
    http.get(`/api/flight-preparations/${id}/detail`).subscribe({ error: () => undefined });
    httpMock.expectOne(`/api/flight-preparations/${id}/detail`).flush(null, { status: 403, statusText: 'Forbidden' });
    http.get(`/api/flight-preparations/${id}`).subscribe({ error: () => undefined });
    httpMock.expectOne(`/api/flight-preparations/${id}`).flush(null, { status: 403, statusText: 'Forbidden' });

    expect(navigate).not.toHaveBeenCalledWith(['/access-denied']);
  });

  it('still routes to access-denied on a 403 when changing a flight', () => {
    const id = '3f2504e0-4f89-41d3-9a0c-0305e82c3301';
    http.put(`/api/flight-preparations/${id}`, {}).subscribe({ error: () => undefined });
    httpMock.expectOne(`/api/flight-preparations/${id}`).flush(null, { status: 403, statusText: 'Forbidden' });

    expect(navigate).toHaveBeenCalledWith(['/access-denied']);
  });

  it('leaves 403 alone for a pending-approval account', () => {
    store['isPendingApproval'] = () => true;
    http.get('/api/flights').subscribe({ error: () => undefined });
    httpMock.expectOne('/api/flights').flush(null, { status: 403, statusText: 'Forbidden' });
    expect(navigate).not.toHaveBeenCalled();
  });
});
