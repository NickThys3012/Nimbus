import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';

import { AuthStore } from './auth.store';
import { AuthenticationService, type CurrentUserDto, type LoginResponseDto } from '../api-client';

const user = (isApproved: boolean, isRejected = false): CurrentUserDto => ({
  id: 'u1',
  email: 'pilot@example.com',
  firstName: 'Nick',
  name: 'Thys',
  roles: ['Pilot'],
  isApproved,
  isRejected,
  requestedAt: '2026-07-26T10:00:00Z',
});

const loginResponse: LoginResponseDto = {
  email: 'pilot@example.com',
  role: 'Pilot',
  isApproved: true,
  isRejected: false,
  requestedAt: '2026-07-26T10:00:00Z',
};

describe('AuthStore approval state', () => {
  function setup(me: CurrentUserDto) {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: AuthenticationService,
          useValue: {
            postApiAuthenticationLogin: () => of(loginResponse),
            getApiAuthenticationMe: () => of(me),
            getApiAuthentication: () => of(undefined),
          },
        },
      ],
    });
    return TestBed.inject(AuthStore);
  }

  it('flags a pending user after login without blocking the login', () => {
    const store = setup(user(false));
    expect(store.isPendingApproval()).toBe(false);
    store.login({ email: 'pilot@example.com', password: 'x', rememberMe: false }).subscribe();
    expect(store.isAuthenticated()).toBe(true);
    expect(store.isApproved()).toBe(false);
    expect(store.isPendingApproval()).toBe(true);
  });

  it('does not flag an approved user', () => {
    const store = setup(user(true));
    store.login({ email: 'pilot@example.com', password: 'x', rememberMe: false }).subscribe();
    expect(store.isApproved()).toBe(true);
    expect(store.isPendingApproval()).toBe(false);
    expect(store.initials()).toBe('NT');
  });

  it('flags a rejected user separately from pending and exposes the request date', () => {
    const store = setup(user(false, true));
    store.login({ email: 'pilot@example.com', password: 'x', rememberMe: false }).subscribe();
    expect(store.isAccessRejected()).toBe(true);
    expect(store.isPendingApproval()).toBe(false);
    expect(store.requestedAt()).toBe('2026-07-26T10:00:00Z');
  });

  it('holds no credential: nothing in the store carries a token', () => {
    const store = setup(user(true));
    store.login({ email: 'pilot@example.com', password: 'x', rememberMe: false }).subscribe();
    expect(Object.keys(store).join(',').toLowerCase()).not.toContain('token');
  });
});

describe('AuthStore restoreSession', () => {
  function setup(me: () => unknown) {
    const getMe = vi.fn(me as never);
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthenticationService, useValue: { getApiAuthenticationMe: getMe } },
      ],
    });
    return { store: TestBed.inject(AuthStore), getMe };
  }

  it('signs the user in when the session cookie is valid', () => {
    const { store } = setup(() => of(user(true)));
    let ok: boolean | undefined;
    store.restoreSession().subscribe((r) => (ok = r));
    expect(ok).toBe(true);
    expect(store.isAuthenticated()).toBe(true);
  });

  it('expired session: resolves false and stays signed out', () => {
    const { store } = setup(() => throwError(() => ({ status: 401 })));
    let ok: boolean | undefined;
    store.restoreSession().subscribe((r) => (ok = r));
    expect(ok).toBe(false);
    expect(store.isAuthenticated()).toBe(false);
  });

  it('shares one request between concurrent callers', () => {
    const pending: Subject<CurrentUserDto>[] = [];
    const { store, getMe } = setup(() => {
      const s = new Subject<CurrentUserDto>();
      pending.push(s);
      return s.asObservable();
    });
    const results: boolean[] = [];

    store.restoreSession().subscribe((ok) => results.push(ok));
    store.restoreSession().subscribe((ok) => results.push(ok));

    expect(getMe).toHaveBeenCalledTimes(1);
    pending[0].next(user(true));
    pending[0].complete();
    expect(results).toEqual([true, true]);
  });

  it('expireSession tells forms first, then forgets the user', () => {
    const { store } = setup(() => of(user(true)));
    store.restoreSession().subscribe();
    const seen: { url: string; stillSignedIn: boolean }[] = [];
    store.sessionExpiring.subscribe((url) => seen.push({ url, stillSignedIn: store.isAuthenticated() }));

    store.expireSession('/flights');

    expect(seen).toEqual([{ url: '/flights', stillSignedIn: true }]);
    expect(store.isAuthenticated()).toBe(false);
  });
});
