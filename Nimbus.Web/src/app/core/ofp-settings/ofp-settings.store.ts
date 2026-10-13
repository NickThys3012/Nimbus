import { inject, Injectable, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';

import { ApiError, type OfpSettingsRequestDto, OfpSettingsService } from '../api-client';

const GENERIC = 'Something went wrong. Please try again.';

/**
 * State of "My OFP settings" on the generated `OfpSettingsService`. The API only ever returns the
 * signed-in user's effective value (own row, else the default), so nothing here addresses a row by id.
 */
@Injectable({ providedIn: 'root' })
export class OfpSettingsStore {
  private readonly api = inject(OfpSettingsService);

  readonly settings = rxResource({ stream: () => this.api.getApiOfpSettings() });

  private readonly _saving = signal(false);
  readonly saving = this._saving.asReadonly();
  private readonly _resetting = signal(false);
  readonly resetting = this._resetting.asReadonly();
  private readonly _saveError = signal<string | null>(null);
  readonly saveError = this._saveError.asReadonly();
  private readonly _fieldError = signal<string | null>(null);
  readonly fieldError = this._fieldError.asReadonly();

  clearErrors(): void {
    this._saveError.set(null);
    this._fieldError.set(null);
  }

  async save(requestBody: OfpSettingsRequestDto): Promise<boolean> {
    if (this._saving()) return false;
    this._saving.set(true);
    this.clearErrors();
    try {
      this.settings.set(await firstValueFrom(this.api.putApiOfpSettings({ requestBody })));
      return true;
    } catch (err) {
      const { field, general } = mapSaveError(err);
      this._fieldError.set(field);
      this._saveError.set(general);
      return false;
    } finally {
      this._saving.set(false);
    }
  }

  /** Removes the user's own setting; the response carries the default now in effect. */
  async reset(): Promise<boolean> {
    if (this._resetting()) return false;
    this._resetting.set(true);
    this.clearErrors();
    try {
      this.settings.set(await firstValueFrom(this.api.deleteApiOfpSettings()));
      return true;
    } catch (err) {
      this._saveError.set(mapSaveError(err).general ?? GENERIC);
      return false;
    } finally {
      this._resetting.set(false);
    }
  }
}

/** A 422 `errors` dictionary only holds the one field, so all its messages go under the input. */
function mapSaveError(err: unknown): { field: string | null; general: string | null } {
  if (err instanceof ApiError) {
    const body = err.body as { title?: string; errors?: Record<string, string[]> } | undefined;
    if (err.status === 422 && body?.errors) {
      const text = Object.values(body.errors).flat().join(' ');
      if (text) return { field: text, general: null };
    }
    if (typeof body?.title === 'string') return { field: null, general: body.title };
  }
  return { field: null, general: GENERIC };
}
