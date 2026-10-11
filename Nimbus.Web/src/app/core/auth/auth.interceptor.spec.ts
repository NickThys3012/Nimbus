import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { vi } from 'vitest';

import { authInterceptor } from './auth.interceptor';
import { AuthStore } from './auth.store';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let navigate: ReturnType<typeof vi.fn>;
  let expireSession: ReturnType<typeof vi.fn>;
  let store: Record<string, unknown>;
  let url: string;

  beforeEach(() => {
    url = '/flights?x=1';
    navigate = vi.fn().mockResolvedValue(true);
    expireSession = vi.fn();
    store = {
      expireSession,
      isPendingApproval: () => false,
      isAccessRejected: () => false,
    };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthStore, useValue: store },
        {
          provide: Router,
          useValue: {
            navigate,
            get url() {
              return url;
            },
          },
        },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('never adds a credential to requests (the session is an HttpOnly cookie)', () => {
    http.get('/api/flights').subscribe();
    const req = httpMock.expectOne('/api/flights');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('expired session: forgets the user and routes to login with the current URL', () => {
    const errors: unknown[] = [];
    http.get('/api/flights').subscribe({ error: (e) => errors.push(e) });

    httpMock.expectOne('/api/flights').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(expireSession).toHaveBeenCalledWith('/flights?x=1');
    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { returnUrl: '/flights?x=1' } });
    expect(errors).toHaveLength(1);
  });

  it('does not loop when already on the login screen', () => {
    url = '/login?returnUrl=%2Fflights';
    http.get('/api/flights').subscribe({ error: () => undefined });
    httpMock.expectOne('/api/flights').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(navigate).not.toHaveBeenCalled();
    expect(expireSession).not.toHaveBeenCalled();
  });

  it.each(['/api/Authentication/login', '/api/Authentication/me'])(
    'leaves a 401 from %s alone (wrong password / "is anyone signed in?" probe)',
    (endpoint) => {
      http.get(endpoint).subscribe({ error: () => undefined });
      httpMock.expectOne(endpoint).flush(null, { status: 401, statusText: 'Unauthorized' });
      expect(navigate).not.toHaveBeenCalled();
      expect(expireSession).not.toHaveBeenCalled();
    },
  );

  it('routes to access-denied on 403', () => {
    http.get('/api/admin').subscribe({ error: () => undefined });
    httpMock.expectOne('/api/admin').flush(null, { status: 403, statusText: 'Forbidden' });
    expect(navigate).toHaveBeenCalledWith(['/access-denied']);
    expect(expireSession).not.toHaveBeenCalled();
  });

  it('leaves 403 alone for a pending-approval account', () => {
    store['isPendingApproval'] = () => true;
    http.get('/api/flights').subscribe({ error: () => undefined });
    httpMock.expectOne('/api/flights').flush(null, { status: 403, statusText: 'Forbidden' });
    expect(navigate).not.toHaveBeenCalled();
  });
});
