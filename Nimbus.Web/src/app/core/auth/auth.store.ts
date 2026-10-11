import { computed, inject, Injectable, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import {
  catchError,
  finalize,
  map,
  Observable,
  of,
  shareReplay,
  Subject,
  switchMap,
  tap,
  throwError,
} from 'rxjs';

import {
  ApiError,
  AuthenticationService,
  type CurrentUserDto,
  type LoginRequestDto,
  type LoginResponseDto,
  RegisterRequestDto
} from '../api-client';

/**
 * Signal-based auth state, built on top of the generated
 * `AuthenticationService` (Observable-based, from `npm run generate:api`).
 *
 * Generated services stay Observable-based (that's what
 * openapi-typescript-codegen produces), so this store is the seam where we
 * convert those calls into signals for the rest of the app to consume.
 *
 * The session itself is an HttpOnly cookie the browser attaches on its own: no credential
 * ever reaches this code. "Signed in" therefore means "`/me` answered with a user", and is
 * re-established after a page reload by `restoreSession()`. The API re-checks the account on
 * every request, so nothing here is a security decision — it only decides what to render.
 */
@Injectable({ providedIn: 'root' })
export class AuthStore {
  /** Reactive lookup, kept in sync with `lookupEmail`. Set `lookupEmail` and
   *  `userLookup.value()` / `.isLoading()` / `.error()` update automatically.
   *  This is the pattern to reach for whenever a query should refetch as its
   *  inputs change (the `resource`/`rxResource` API is Angular's
   *  signal-native replacement for manual Observable subscriptions on GETs).
   */
  readonly lookupEmail = signal<string | null>(null);
  /** The signed-in account (identity, roles, approval state), or null while signed out. */
  private readonly _user = signal<CurrentUserDto | null>(null);
  readonly user = this._user.asReadonly();
  readonly isAuthenticated = computed(() => this._user() !== null);
  readonly roles = computed(() => this._user()?.roles ?? []);
  readonly role = computed(() => this._user()?.roles[0] ?? null);
  readonly email = computed(() => this._user()?.email ?? null);
  readonly isApproved = computed(() => this._user()?.isApproved ?? false);
  readonly isRejected = computed(() => this._user()?.isRejected ?? false);
  /** ISO timestamp of when the account was requested. */
  readonly requestedAt = computed(() => this._user()?.requestedAt ?? null);
  /** True for a signed-in user still awaiting admin approval (drives the "pending" notice). */
  readonly isPendingApproval = computed(
    () => this.isAuthenticated() && !this.isApproved() && !this.isRejected(),
  );
  /** True for a signed-in user an admin has rejected (drives the "no access" card). */
  readonly isAccessRejected = computed(() => this.isAuthenticated() && this.isRejected());
  readonly firstName = computed(() => this._user()?.firstName ?? null);
  readonly name = computed(() => this._user()?.name ?? null);
  /** e.g. "N. Thys" */
  readonly abbreviatedName = computed(() => {
    const first = this.firstName();
    const last = this.name();
    if (!first && !last) return null;
    if (!first) return last;
    if (!last) return first;
    return `${first[0].toUpperCase()}. ${last}`;
  });
  /** e.g. "NT" */
  readonly initials = computed(() => {
    const first = this.firstName();
    const last = this.name();
    const f = first?.[0]?.toUpperCase() ?? '';
    const l = last?.[0]?.toUpperCase() ?? '';
    return f + l || null;
  });

  /**
   * Emits (with the URL being left) just before the app sends the user to login because the session
   * ended. Forms subscribe to stash unsaved work (see `DraftService`) while they still exist.
   */
  readonly sessionExpiring = new Subject<string>();

  private readonly authApi = inject(AuthenticationService);
  readonly userLookup = rxResource({
    params: () => this.lookupEmail(),
    stream: ({ params: email }) => this.authApi.getApiAuthentication({ email: email ?? undefined }),
  });
  private readonly _isLoading = signal(false);
  readonly isLoading = this._isLoading.asReadonly();
  private readonly _error = signal<string | null>(null);
  readonly error = this._error.asReadonly();
  login(request: LoginRequestDto): Observable<LoginResponseDto> {
    this._isLoading.set(true);
    this._error.set(null);

    return this.authApi.postApiAuthenticationLogin({ requestBody: request }).pipe(
      // The login response sets the session cookie; `/me` then supplies the full profile (name, roles).
      switchMap((response) =>
        this.authApi.getApiAuthenticationMe().pipe(
          tap((user) => this._user.set(user)),
          map(() => response),
        ),
      ),
      finalize(() => this._isLoading.set(false)),
      tap(() => this._error.set(null)),
      catchError((err) => {
        this._error.set(err?.message ?? 'Login failed');
        return throwError(() => err);
      }),
    );
  }

  register(request: RegisterRequestDto): Observable<void> {
    this._isLoading.set(true);
    this._error.set(null);

    return this.authApi.postApiAuthenticationRegister({ requestBody: request }).pipe(
      finalize(() => this._isLoading.set(false)),
      tap(() => this._error.set(null)),
      catchError((err) => {
        if (this.isDuplicateRegistrationError(err)) {
          this._error.set(null);
          return of(void 0);
        }

        this._error.set('Registration failed. Please try again.');
        return throwError(() => err);
      }),
    );
  }

  requestNewVerificationEmail(email: string): Observable<void> {
    this._isLoading.set(true);
    this._error.set(null);

    return this.authApi
      .postApiAuthenticationResendVerificationEmail({ requestBody: { email } })
      .pipe(
        map(() => void 0),
        catchError((err) => {
          if (this.isUserNotFoundError(err)) {
            this._error.set(null);
            return of(void 0);
          }

          this._error.set(err?.body?.title ?? err?.message ?? 'Request failed');
          return throwError(() => err);
        }),
        finalize(() => this._isLoading.set(false)),
      );
  }

  private restoreInFlight: Observable<boolean> | null = null;

  /**
   * Asks the API who the session cookie belongs to. Used on app start (to restore a session after a
   * page reload) and by `authGuard` before giving up on a route. Never throws — resolves to `false`
   * (and clears local state) when there is no valid session. Concurrent callers share one request.
   */
  restoreSession(): Observable<boolean> {
    if (this.restoreInFlight) {
      return this.restoreInFlight;
    }

    const request = this.authApi.getApiAuthenticationMe().pipe(
      tap((user) => this._user.set(user)),
      map(() => true),
      catchError(() => {
        this.clearSession();
        return of(false);
      }),
      finalize(() => (this.restoreInFlight = null)),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    this.restoreInFlight = request;
    return request;
  }

  /** The API said 401 mid-session: forget the user locally and tell forms to stash unsaved work. */
  expireSession(returnUrl: string): void {
    this.sessionExpiring.next(returnUrl);
    this.clearSession();
  }

  logout(): void {
    this.authApi.postApiAuthenticationLogout().subscribe({
      // Clear local session state whether the API call succeeds —
      // the user should be logged out client-side regardless.
      next: () => this.clearSession(),
      error: () => this.clearSession(),
    });
  }

  private isDuplicateRegistrationError(err: unknown): boolean {
    const apiError = err as Partial<ApiError> | null;
    if (!apiError) {
      return false;
    }

    if (apiError.status === 409) {
      return true;
    }

    const bodyTitle =
      typeof apiError.body?.title === 'string' ? apiError.body.title.toLowerCase() : '';
    const message = typeof apiError.message === 'string' ? apiError.message.toLowerCase() : '';

    return (
      (bodyTitle.includes('email') && bodyTitle.includes('already')) ||
      (message.includes('email') && message.includes('already'))
    );
  }

  private isUserNotFoundError(err: unknown): boolean {
    const apiError = err as Partial<ApiError> | null;
    if (!apiError) {
      return false;
    }

    if (apiError.status === 404) {
      return true;
    }

    const bodyTitle =
      typeof apiError.body?.title === 'string' ? apiError.body.title.toLowerCase() : '';
    const message = typeof apiError.message === 'string' ? apiError.message.toLowerCase() : '';

    return (
      (bodyTitle.includes('user') && bodyTitle.includes('not found')) ||
      (message.includes('user') && message.includes('not found'))
    );
  }

  private clearSession(): void {
    this._user.set(null);
  }
}
