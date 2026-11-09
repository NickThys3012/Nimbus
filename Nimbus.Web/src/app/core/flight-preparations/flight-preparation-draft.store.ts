import { inject, Injectable } from '@angular/core';

import { AuthStore } from '../auth/auth.store';
import { LOCAL_STORAGE } from '../../services/theme-service';

export interface FlightPreparationDraft<T> {
  value: T;
  updatedAt: string;
}

/**
 * Client-side persistence for the nine-section flight-preparation dossier (issue #32). There is no
 * save/load API yet (#31 only shipped the schema; the real endpoints are future issues in the
 * #40/#51 family) — until then, "saving" a dossier means writing it here so a refresh, a dropped
 * connection or an accidental tab close never loses a pilot's work.
 *
 * **Per-user isolation**: every key is namespaced with the signed-in user's email (the one piece
 * of identity available synchronously right after login/restore, see `AuthStore.email`), so two
 * different Nimbus accounts signed in on the same browser can never read or overwrite each other's
 * drafts. Swap `localStorage` for the real API client here once a save endpoint exists — the
 * public shape (`load`/`save`/`remove`/`newId`) is designed to stay the same.
 */
@Injectable({ providedIn: 'root' })
export class FlightPreparationDraftStore {
  private static readonly prefix = 'nimbus.flight-prep.';
  /** A second, separate bucket for the continuous (debounced, every keystroke) autosave, kept
   *  apart from the "saved" bucket above so the form can tell a page reload mid-edit (an autosave
   *  that diverges from what was last explicitly saved) apart from a flight that's simply being
   *  reopened as last saved — see `FlightPreparationFormPage`'s draft-restored banner (issue #32
   *  mockup frame 3A). */
  private static readonly autosavePrefix = 'nimbus.flight-prep.autosave.';

  private readonly auth = inject(AuthStore);
  private readonly storage = inject(LOCAL_STORAGE);

  /** A fresh client-side id for a dossier that doesn't have one yet. */
  newId(): string {
    return crypto.randomUUID();
  }

  load<T>(flightId: string): FlightPreparationDraft<T> | null {
    return this.read<T>(this.key(flightId));
  }

  /** Persists the dossier's current value and returns the timestamp it was saved at. */
  save<T>(flightId: string, value: T): string {
    return this.write(this.key(flightId), value);
  }

  remove(flightId: string): void {
    this.erase(this.key(flightId));
  }

  /** The continuous per-keystroke backup — written on every debounced change so a refresh never
   *  loses work, independent of whether the pilot has explicitly saved yet. */
  loadAutosave<T>(flightId: string): FlightPreparationDraft<T> | null {
    return this.read<T>(this.autosaveKey(flightId));
  }

  saveAutosave<T>(flightId: string, value: T): string {
    return this.write(this.autosaveKey(flightId), value);
  }

  removeAutosave(flightId: string): void {
    this.erase(this.autosaveKey(flightId));
  }

  private read<T>(key: string): FlightPreparationDraft<T> | null {
    try {
      const raw = this.storage?.getItem(key) ?? null;
      return raw === null ? null : (JSON.parse(raw) as FlightPreparationDraft<T>);
    } catch {
      return null;
    }
  }

  private write<T>(key: string, value: T): string {
    const updatedAt = new Date().toISOString();
    try {
      this.storage?.setItem(key, JSON.stringify({ value, updatedAt }));
    } catch {
      // Best effort: losing the draft must never crash the form (private mode, quota, ...).
    }
    return updatedAt;
  }

  private erase(key: string): void {
    try {
      this.storage?.removeItem(key);
    } catch {
      // Nothing to clean up.
    }
  }

  private key(flightId: string): string {
    const owner = this.auth.email() ?? 'anonymous';
    return `${FlightPreparationDraftStore.prefix}${owner}.${flightId}`;
  }

  private autosaveKey(flightId: string): string {
    const owner = this.auth.email() ?? 'anonymous';
    return `${FlightPreparationDraftStore.autosavePrefix}${owner}.${flightId}`;
  }
}
