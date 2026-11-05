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

  private readonly auth = inject(AuthStore);
  private readonly storage = inject(LOCAL_STORAGE);

  /** A fresh client-side id for a dossier that doesn't have one yet. */
  newId(): string {
    return crypto.randomUUID();
  }

  load<T>(flightId: string): FlightPreparationDraft<T> | null {
    try {
      const raw = this.storage?.getItem(this.key(flightId)) ?? null;
      return raw === null ? null : (JSON.parse(raw) as FlightPreparationDraft<T>);
    } catch {
      return null;
    }
  }

  /** Persists the dossier's current value and returns the timestamp it was saved at. */
  save<T>(flightId: string, value: T): string {
    const updatedAt = new Date().toISOString();
    try {
      this.storage?.setItem(this.key(flightId), JSON.stringify({ value, updatedAt }));
    } catch {
      // Best effort: losing the draft must never crash the form (private mode, quota, ...).
    }
    return updatedAt;
  }

  remove(flightId: string): void {
    try {
      this.storage?.removeItem(this.key(flightId));
    } catch {
      // Nothing to clean up.
    }
  }

  private key(flightId: string): string {
    const owner = this.auth.email() ?? 'anonymous';
    return `${FlightPreparationDraftStore.prefix}${owner}.${flightId}`;
  }
}
