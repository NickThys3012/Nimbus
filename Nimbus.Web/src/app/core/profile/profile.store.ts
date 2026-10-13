import { inject, Injectable, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';

import { ApiError, ProfileService, type ProfileRequestDto } from '../api-client';

export type ProfileField = keyof ProfileRequestDto;
export type ProfileFieldErrors = Partial<Record<ProfileField, string>>;

const GENERIC = 'Something went wrong. Please try again.';

/**
 * State of "My profile" (operator name and PIC weight) on the generated `ProfileService`. The API only
 * ever returns the signed-in user's own profile, so nothing here addresses a row by id.
 */
@Injectable({ providedIn: 'root' })
export class ProfileStore {
  private readonly api = inject(ProfileService);

  readonly profile = rxResource({ stream: () => this.api.getApiProfile() });

  private readonly _saving = signal(false);
  readonly saving = this._saving.asReadonly();
  private readonly _saveError = signal<string | null>(null);
  readonly saveError = this._saveError.asReadonly();
  private readonly _fieldErrors = signal<ProfileFieldErrors>({});
  readonly fieldErrors = this._fieldErrors.asReadonly();

  clearErrors(): void {
    this._saveError.set(null);
    this._fieldErrors.set({});
  }

  async save(requestBody: ProfileRequestDto): Promise<boolean> {
    if (this._saving()) return false;
    this._saving.set(true);
    this.clearErrors();
    try {
      this.profile.set(await firstValueFrom(this.api.putApiProfile({ requestBody })));
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
}

/** Splits a 422 `errors` dictionary (keys like "Profile.PicWeightKg") per form field. */
function mapSaveError(err: unknown): { fields: ProfileFieldErrors; general: string | null } {
  if (err instanceof ApiError) {
    const body = err.body as { title?: string; errors?: Record<string, string[]> } | undefined;
    if (err.status === 422 && body?.errors) {
      const fields: ProfileFieldErrors = {};
      const general: string[] = [];
      for (const [key, messages] of Object.entries(body.errors)) {
        const text = messages.join(' ');
        const prop = key.split('.').pop()!.toLowerCase();
        const field = (['operatorName', 'picWeightKg'] as ProfileField[]).find((f) => f.toLowerCase() === prop);
        if (field) fields[field] = fields[field] ? `${fields[field]} ${text}` : text;
        else general.push(text);
      }
      return { fields, general: general.length > 0 ? general.join(' ') : null };
    }
    if (typeof body?.title === 'string') return { fields: {}, general: body.title };
  }
  return { fields: {}, general: GENERIC };
}
