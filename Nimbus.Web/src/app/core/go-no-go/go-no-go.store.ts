import { inject, Injectable, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';

import { ApiError, type GoNoGoThresholdsRequestDto, GoNoGoThresholdsService } from '../api-client';

/** The form/request fields, in display order. */
export type ThresholdField = keyof GoNoGoThresholdsRequestDto;
export type ThresholdFieldErrors = Partial<Record<ThresholdField, string>>;

const FIELDS: ThresholdField[] = ['windYellowKt', 'windRedKt', 'visibilityYellowKm', 'visibilityRedKm', 'capeYellow', 'capeRed'];

/**
 * State of "My Go/No-Go thresholds" on the generated `GoNoGoThresholdsService`. The API only ever
 * returns the signed-in user's effective values (own row, else the default), so nothing here
 * addresses a row by id.
 */
@Injectable({ providedIn: 'root' })
export class GoNoGoStore {
  private readonly api = inject(GoNoGoThresholdsService);

  readonly thresholds = rxResource({ stream: () => this.api.getApiGoNoGoThresholds() });

  private readonly _saving = signal(false);
  readonly saving = this._saving.asReadonly();
  private readonly _resetting = signal(false);
  readonly resetting = this._resetting.asReadonly();
  private readonly _saveError = signal<string | null>(null);
  readonly saveError = this._saveError.asReadonly();
  private readonly _fieldErrors = signal<ThresholdFieldErrors>({});
  readonly fieldErrors = this._fieldErrors.asReadonly();

  clearErrors(): void {
    this._saveError.set(null);
    this._fieldErrors.set({});
  }

  async save(requestBody: GoNoGoThresholdsRequestDto): Promise<boolean> {
    if (this._saving()) return false;
    this._saving.set(true);
    this.clearErrors();
    try {
      this.thresholds.set(await firstValueFrom(this.api.putApiGoNoGoThresholds({ requestBody })));
      return true;
    } catch (err) {
      const { fields, general } = mapSaveError(err);
      this._fieldErrors.set(fields);
      this._saveError.set(general);
      return false;
    } finally {
      this._saving.set(false);
    }
  }

  /** Removes the user's own thresholds; the response carries the defaults now in effect. */
  async reset(): Promise<boolean> {
    if (this._resetting()) return false;
    this._resetting.set(true);
    this.clearErrors();
    try {
      this.thresholds.set(await firstValueFrom(this.api.deleteApiGoNoGoThresholds()));
      return true;
    } catch (err) {
      this._saveError.set(mapSaveError(err).general ?? GENERIC);
      return false;
    } finally {
      this._resetting.set(false);
    }
  }
}

const GENERIC = 'Something went wrong. Please try again.';

/** Splits a 422 `errors` dictionary (keys like "Thresholds.WindRedKt") per form field. */
function mapSaveError(err: unknown): { fields: ThresholdFieldErrors; general: string | null } {
  if (err instanceof ApiError) {
    const body = err.body as { title?: string; errors?: Record<string, string[]> } | undefined;
    if (err.status === 422 && body?.errors) {
      const fields: ThresholdFieldErrors = {};
      const general: string[] = [];
      for (const [key, messages] of Object.entries(body.errors)) {
        const text = messages.join(' ');
        const prop = key.split('.').pop()!.toLowerCase();
        const field = FIELDS.find((f) => f.toLowerCase() === prop);
        if (field) fields[field] = fields[field] ? `${fields[field]} ${text}` : text;
        else general.push(text);
      }
      return { fields, general: general.length > 0 ? general.join(' ') : null };
    }
    if (typeof body?.title === 'string') return { fields: {}, general: body.title };
  }
  return { fields: {}, general: GENERIC };
}
