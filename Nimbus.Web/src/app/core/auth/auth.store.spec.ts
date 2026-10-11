import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';

import { AuthStore } from './auth.store';
import { AuthenticationService, type LoginResponseDto } from '../api-client';

describe('AuthStore approval state', () => {
  const login = (isApproved: boolean, isRejected = false): LoginResponseDto => ({
    accessToken: 't',
    expiration: '2099-01-01T00:00:00Z',
    email: 'pilot@example.com',
    role: 'Pilot',
    isApproved,
    isRejected,
    requestedAt: '2026-07-26T10:00:00Z',
  });

  function setup(response: LoginResponseDto) {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: AuthenticationService,
          useValue: {
            postApiAuthenticationLogin: () => of(response),
            getApiAuthenticationMe: () => new Promise(() => undefined) as never,
            getApiAuthentication: () => of(undefined),
          },
        },
      ],
    });
    return TestBed.inject(AuthStore);
  }

  it('flags a pending user after login without blocking the login', () => {
    const store = setup(login(false));
    expect(store.isPendingApproval()).toBe(false);
    store.login({ email: 'pilot@example.com', password: 'x', rememberMe: false }).subscribe();
    expect(store.isAuthenticated()).toBe(true);
    expect(store.isApproved()).toBe(false);
    expect(store.isPendingApproval()).toBe(true);
  });

  it('does not flag an approved user', () => {
    const store = setup(login(true));
    store.login({ email: 'pilot@example.com', password: 'x', rememberMe: false }).subscribe();
    expect(store.isApproved()).toBe(true);
    expect(store.isPendingApproval()).toBe(false);
  });

  it('flags a rejected user separately from pending and exposes the request date', () => {
    const store = setup(login(false, true));
    store.login({ email: 'pilot@example.com', password: 'x', rememberMe: false }).subscribe();
    expect(store.isAccessRejected()).toBe(true);
    expect(store.isPendingApproval()).toBe(false);
    expect(store.requestedAt()).toBe('2026-07-26T10:00:00Z');
  });
});

describe('AuthStore restoreSession', () => {
  const response: LoginResponseDto = {
    accessToken: 't',
    expiration: '2099-01-01T00:00:00Z',
    email: 'pilot@example.com',
    role: 'Pilot',
    isApproved: true,
    isRejected: false,
    requestedAt: '2026-07-26T10:00:00Z',
  };

  function setup() {
    const pending: Subject<LoginResponseDto>[] = [];
    const refresh = vi.fn(() => {
      const subject = new Subject<LoginResponseDto>();
      pending.push(subject);
      return subject.asObservable();
    });
    TestBed.configureTestingModule({
      providers: [
        {
          provide: AuthenticationService,
          useValue: {
            postApiAuthenticationRefresh: refresh,
            getApiAuthenticationMe: () => new Promise(() => undefined) as never,
          },
        },
      ],
    });
    return { store: TestBed.inject(AuthStore), refresh, pending };
  }

  it('shares one refresh request between concurrent callers (refresh tokens are single-use)', () => {
    const { store, refresh, pending } = setup();
    const results: boolean[] = [];

    store.restoreSession().subscribe((ok) => results.push(ok));
    store.restoreSession().subscribe((ok) => results.push(ok));
    store.restoreSession().subscribe((ok) => results.push(ok));

    expect(refresh).toHaveBeenCalledTimes(1);

    pending[0].next(response);
    pending[0].complete();
    expect(results).toEqual([true, true, true]);
  });

  it('starts a new refresh once the previous one has finished', () => {
    const { store, refresh, pending } = setup();

    store.restoreSession().subscribe();
    pending[0].next(response);
    pending[0].complete();

    store.restoreSession().subscribe();
    expect(refresh).toHaveBeenCalledTimes(2);
  });
});
