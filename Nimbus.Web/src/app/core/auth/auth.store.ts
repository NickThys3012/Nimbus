import { computed, inject, Injectable, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { catchError, finalize, map, Observable, of, tap, throwError } from 'rxjs';

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
  private readonly authApi = inject(AuthenticationService);
  readonly userLookup = rxResource({
    params: () => this.lookupEmail(),
    stream: ({ params: email }) => this.authApi.getApiAuthentication({ email: email ?? undefined }),
  });
  // queries, so we drive their state with plain writable signals.
  private readonly _accessToken = signal<string | null>(null);
  readonly accessToken = this._accessToken.asReadonly();
  readonly isAuthenticated = computed(() => this._accessToken() !== null);
  /**
   * Identity, roles and approval state for the signed-in user (issue #15) — refetches
   * automatically whenever `_accessToken` changes (login/refresh/restoreSession), and is
   * skipped entirely while signed out since `params` resolves to `undefined` then. This is
   * what lets guards/the shell tell an approved pilot apart from one still pending admin
   * approval, without that state ever being trusted from the client-only route guard.
   */
  readonly currentUser = rxResource<CurrentUserDto, string | undefined>({
    params: () => this._accessToken() ?? undefined,
    stream: () => this.authApi.getApiAuthenticationMe(),
  });
  // Mutations (login/logout/refresh) are one-off actions, not reactive
  readonly roles = computed(() => this.currentUser.value()?.roles ?? []);
  readonly isApproved = computed(() => this.currentUser.value()?.isApproved ?? false);
  private readonly _email = signal<string | null>(null);
  readonly email = this._email.asReadonly();
  private readonly _role = signal<string | null>(null);
  readonly role = this._role.asReadonly();
  private readonly _isLoading = signal(false);
  readonly isLoading = this._isLoading.asReadonly();
  private readonly _error = signal<string | null>(null);
  readonly error = this._error.asReadonly();
  /** Set once a restore/refresh attempt has resolved, so guards know
   *  whether `isAuthenticated` reflects a real answer yet (relevant right
   *  after a full page reload, before the access token in memory has been
   *  re-hydrated from the httpOnly refresh cookie). */
  private readonly _isRestored = signal(false);
  readonly isRestored = this._isRestored.asReadonly();

  login(request: LoginRequestDto): Observable<LoginResponseDto> {
    this._isLoading.set(true);
    this._error.set(null);

    return this.authApi.postApiAuthenticationLogin({ requestBody: request }).pipe(
      finalize(() => this._isLoading.set(false)),
      tap((response) => this.applyLoginResponse(response)),
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

  refresh(): void {
    this.restoreSession().subscribe();
  }

  /**
   * Silently attempts to obtain a new access token from the httpOnly
   * refresh-token cookie. Used on app start (to restore a session after a
   * page reload, since the access token itself only ever lives in memory)
   * and by `authGuard`/the auth interceptor before giving up on a route or
   * request. Never throws — resolves to `false` on any failure.
   */
  restoreSession(): Observable<boolean> {
    return this.authApi.postApiAuthenticationRefresh().pipe(
      tap((response) => this.applyLoginResponse(response)),
      map(() => true),
      catchError(() => {
        this.clearSession();
        return of(false);
      }),
      finalize(() => this._isRestored.set(true)),
    );
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

  private applyLoginResponse(response: LoginResponseDto): void {
    this._accessToken.set(response.accessToken);
    this._email.set(response.email);
    this._role.set(response.role);
  }

  private clearSession(): void {
    this._accessToken.set(null);
    this._email.set(null);
    this._role.set(null);
  }
}
