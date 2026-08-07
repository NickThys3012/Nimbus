import { Injectable, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';

import {
  AuthenticationService,
  type LoginRequestDto,
  type LoginResponseDto,
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
  private readonly authApi = inject(AuthenticationService);

  // Mutations (login/logout/refresh) are one-off actions, not reactive
  // queries, so we drive their state with plain writable signals.
  private readonly _accessToken = signal<string | null>(null);
  private readonly _email = signal<string | null>(null);
  private readonly _role = signal<string | null>(null);
  private readonly _isLoading = signal(false);
  private readonly _error = signal<string | null>(null);

  readonly accessToken = this._accessToken.asReadonly();
  readonly email = this._email.asReadonly();
  readonly role = this._role.asReadonly();
  readonly isLoading = this._isLoading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly isAuthenticated = computed(() => this._accessToken() !== null);

  /** Reactive lookup, kept in sync with `lookupEmail`. Set `lookupEmail` and
   *  `userLookup.value()` / `.isLoading()` / `.error()` update automatically.
   *  This is the pattern to reach for whenever a query should refetch as its
   *  inputs change (the `resource`/`rxResource` API is Angular's
   *  signal-native replacement for manual Observable subscriptions on GETs).
   */
  readonly lookupEmail = signal<string | null>(null);
  readonly userLookup = rxResource({
    params: () => this.lookupEmail(),
    stream: ({ params: email }) =>
      this.authApi.getApiAuthentication({ email: email ?? undefined }),
  });

  login(request: LoginRequestDto): void {
    this._isLoading.set(true);
    this._error.set(null);

    this.authApi
      .postApiAuthenticationLogin({ requestBody: request })
      .pipe(finalize(() => this._isLoading.set(false)))
      .subscribe({
        next: (response) => this.applyLoginResponse(response),
        error: (err) => this._error.set(err?.message ?? 'Login failed'),
      });
  }

  refresh(): void {
    this.authApi.postApiAuthenticationRefresh().subscribe({
      next: (response) => this.applyLoginResponse(response),
      error: (err) => this._error.set(err?.message ?? 'Session refresh failed'),
    });
  }

  logout(): void {
    this.authApi.postApiAuthenticationLogout().subscribe({
      // Clear local session state whether or not the API call succeeds —
      // the user should be logged out client-side regardless.
      next: () => this.clearSession(),
      error: () => this.clearSession(),
    });
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
