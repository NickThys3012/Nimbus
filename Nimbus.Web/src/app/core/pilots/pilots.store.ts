import { computed, inject, Injectable, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';

import { ApiError, PilotsService, type PilotRequestDto } from '../api-client';

/**
 * Signal-based state for "My pilots", built on the generated `PilotsService` (same seam as
 * `BalloonsStore`). Everything the API returns is already scoped to the signed-in user and sorted
 * by name, which is the order the flight-preparation picker uses.
 */
@Injectable({ providedIn: 'root' })
export class PilotsStore {
  private readonly api = inject(PilotsService);

  readonly pilots = rxResource({ stream: () => this.api.getApiPilots() });
  readonly list = computed(() => (this.pilots.hasValue() ? this.pilots.value() : []));

  private readonly _saving = signal(false);
  readonly saving = this._saving.asReadonly();
  private readonly _saveError = signal<string | null>(null);
  /** General (not field-specific) save failure. */
  readonly saveError = this._saveError.asReadonly();
  private readonly _fieldErrors = signal<PilotFieldErrors>({});
  /** Server-side validation messages per form field (`name`, `weightKg`). */
  readonly fieldErrors = this._fieldErrors.asReadonly();
  private readonly _deleting = signal(false);
  readonly deleting = this._deleting.asReadonly();
  private readonly _listError = signal<string | null>(null);
  readonly listError = this._listError.asReadonly();

  resetSave(): void {
    this._saveError.set(null);
    this._fieldErrors.set({});
  }

  /** Creates (`id` null) or updates a pilot. Resolves true on success; failures land in `saveError`. */
  async save(id: string | null, requestBody: PilotRequestDto): Promise<boolean> {
    if (this._saving()) return false;
    this._saving.set(true);
    this.resetSave();
    try {
      await firstValueFrom(id === null ? this.api.postApiPilots({ requestBody }) : this.api.putApiPilots({ id, requestBody }));
      this.pilots.reload();
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

  async remove(id: string): Promise<boolean> {
    if (this._deleting()) return false;
    this._deleting.set(true);
    this._listError.set(null);
    try {
      await firstValueFrom(this.api.deleteApiPilots({ id }));
      this.pilots.reload();
      return true;
    } catch (err) {
      this._listError.set(describe(err));
      this.pilots.reload();
      return false;
    } finally {
      this._deleting.set(false);
    }
  }
}

export interface PilotFieldErrors {
  name?: string;
  weightKg?: string;
}

const GENERIC = 'Something went wrong. Please try again.';

/** Splits a 422 `errors` dictionary (keyed by the request property, e.g. "Name", "WeightKg") per form field. */
function mapSaveError(err: unknown): { fields: PilotFieldErrors; general: string | null } {
  if (err instanceof ApiError) {
    const body = err.body as { title?: string; errors?: Record<string, string[]> } | undefined;
    if (err.status === 422 && body?.errors) {
      const fields: PilotFieldErrors = {};
      const general: string[] = [];
      for (const [key, messages] of Object.entries(body.errors)) {
        const text = messages.join(' ');
        // The API keys errors by command path, e.g. "Pilot.WeightKg": match on the last segment.
        const prop = key.split('.').pop()!.toLowerCase();
        const field = prop === 'name' ? 'name' : prop === 'weightkg' ? 'weightKg' : null;
        if (field) fields[field] = fields[field] ? `${fields[field]} ${text}` : text;
        else general.push(text);
      }
      return { fields, general: general.length > 0 ? general.join(' ') : null };
    }
    if (typeof body?.title === 'string') return { fields: {}, general: body.title };
  }
  return { fields: {}, general: GENERIC };
}

function describe(err: unknown): string {
  if (err instanceof ApiError) {
    const body = err.body as { title?: string } | undefined;
    if (typeof body?.title === 'string') return body.title;
  }
  return GENERIC;
}
