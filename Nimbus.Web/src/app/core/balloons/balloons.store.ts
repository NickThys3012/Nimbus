import { computed, inject, Injectable, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';

import {
  ApiError,
  type BalloonDto,
  type BalloonRequestDto,
  BalloonsService,
  type BalloonUsageDto,
} from '../api-client';

/**
 * Signal-based state for "My balloons", built on the generated `BalloonsService` (same seam as
 * `AuthStore`): the list is an `rxResource`, mutations are promise-returning actions that drive
 * busy/error signals so components never subscribe to the API directly. Everything the API returns
 * is already scoped to the signed-in user.
 */
@Injectable({ providedIn: 'root' })
export class BalloonsStore {
  private readonly api = inject(BalloonsService);

  readonly balloons = rxResource({ stream: () => this.api.getApiBalloons() });
  readonly list = computed(() => (this.balloons.hasValue() ? this.balloons.value() : []));
  readonly isLoading = computed(() => this.balloons.isLoading());
  readonly hasLoaded = computed(() => this.balloons.hasValue());
  readonly loadFailed = computed(() => this.balloons.error() !== undefined);

  private readonly _saving = signal(false);
  readonly saving = this._saving.asReadonly();
  /** A 409 on save: the registration already exists for this user. */
  private readonly _duplicate = signal(false);
  readonly duplicate = this._duplicate.asReadonly();
  private readonly _saveError = signal<string | null>(null);
  readonly saveError = this._saveError.asReadonly();

  private readonly _checkingId = signal<string | null>(null);
  /** Id whose usage is being looked up (delete clicked, dialog not open yet). */
  readonly checkingId = this._checkingId.asReadonly();
  private readonly _deleting = signal(false);
  readonly deleting = this._deleting.asReadonly();
  private readonly _listError = signal<string | null>(null);
  readonly listError = this._listError.asReadonly();

  /** Clears the save feedback, e.g. when a form is opened. */
  resetSave(): void {
    this._duplicate.set(false);
    this._saveError.set(null);
  }

  clearListError(): void {
    this._listError.set(null);
  }

  /** Creates (`id` null) or updates a balloon. Resolves true on success; failures land in the error signals. */
  async save(id: string | null, requestBody: BalloonRequestDto): Promise<boolean> {
    if (this._saving()) return false;
    this._saving.set(true);
    this.resetSave();
    try {
      await firstValueFrom(id === null ? this.api.postApiBalloons({ requestBody }) : this.api.putApiBalloons({ id, requestBody }));
      this.balloons.reload();
      return true;
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) this._duplicate.set(true);
      else this._saveError.set(describe(err));
      return false;
    } finally {
      this._saving.set(false);
    }
  }

  /** The flights using a balloon, or null when the lookup failed (see `listError`). */
  async usage(balloon: BalloonDto): Promise<BalloonUsageDto | null> {
    if (this._checkingId()) return null;
    this._checkingId.set(balloon.id);
    this._listError.set(null);
    try {
      return await firstValueFrom(this.api.getApiBalloonsUsage({ id: balloon.id }));
    } catch (err) {
      this._listError.set(describe(err));
      return null;
    } finally {
      this._checkingId.set(null);
    }
  }

  /** Deletes a balloon. The server refuses (409) while flights use it; that message lands in `listError`. */
  async remove(id: string): Promise<boolean> {
    if (this._deleting()) return false;
    this._deleting.set(true);
    this._listError.set(null);
    try {
      await firstValueFrom(this.api.deleteApiBalloons({ id }));
      this.balloons.reload();
      return true;
    } catch (err) {
      this._listError.set(describe(err));
      this.balloons.reload();
      return false;
    } finally {
      this._deleting.set(false);
    }
  }
}

function describe(err: unknown): string {
  if (err instanceof ApiError) {
    const body = err.body as { title?: string; errors?: Record<string, string[]> } | undefined;
    if (err.status === 422 && body?.errors) return Object.values(body.errors).flat().join(' ');
    if (typeof body?.title === 'string') return body.title;
  }
  return 'Something went wrong. Please try again.';
}
