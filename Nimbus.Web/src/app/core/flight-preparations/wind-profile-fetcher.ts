import { computed, effect, inject, Injectable, signal, untracked } from '@angular/core';
import type { FormArray, FormGroup } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { ApiError, WeatherService, type WindProfileDto, type WindProfileLevelDto } from '../api-client';

/** Models verified to return every pressure level and the low-altitude winds (mirrors the API's list, issue #46). */
export const WIND_PROFILE_MODELS = [
  'icon_seamless',
  'icon_d2',
  'icon_eu',
  'icon_global',
  'meteofrance_seamless',
  'meteofrance_arome_france',
  'meteofrance_arpege_europe',
  'meteofrance_arpege_world',
  'cma_grapes_global',
  'ukmo_uk_deterministic_2km',
  'best_match',
] as const;
export const DEFAULT_WIND_PROFILE_MODEL = 'icon_seamless';

export const NO_COORDINATES_MESSAGE = 'Pick a launch location in section 01 to fetch the wind by altitude.';

export interface WindFetchOutcome {
  kind: 'ok' | 'failed';
  message: string;
}

interface RowValue {
  altitudeM: number;
  directionDeg: number;
  speedKt: number;
  temperatureC: number;
}
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type WindArray = FormArray<any>;

/**
 * Fetches the wind by altitude for the launch location into the wind-on-altitude table (issue #46). It only fills
 * rows: a failed fetch changes nothing, and fetched rows never replace rows the pilot typed or edited without a
 * confirmation (`pending` is the fetched profile waiting for that answer).
 */
@Injectable()
export class WindProfileFetcher {
  private readonly weather = inject(WeatherService);

  readonly coordinates = signal<{ latitude: number; longitude: number } | null>(null);
  /** The flight's take-off hour (UTC ISO), or null for the current hour. */
  readonly hourUtc = signal<string | null>(null);
  readonly model = signal<string>(DEFAULT_WIND_PROFILE_MODEL);
  readonly fetching = signal(false);
  readonly outcome = signal<WindFetchOutcome | null>(null);
  /** A fetched profile that would replace the pilot's own rows, waiting for the pilot to confirm. */
  readonly pending = signal<WindProfileDto | null>(null);

  readonly blockedReason = computed(() => (this.coordinates() ? null : NO_COORDINATES_MESSAGE));

  /** What the last fetch put in the table: rows still equal to it are not the pilot's own. */
  private lastFetched: RowValue[] | null = null;

  constructor() {
    // A result belongs to the place it was fetched for.
    effect(() => {
      this.coordinates();
      untracked(() => {
        this.outcome.set(null);
        this.pending.set(null);
      });
    });
  }

  async fetch(rows: WindArray, createRow: () => FormGroup): Promise<void> {
    const at = this.coordinates();
    if (!at || this.fetching()) return;

    this.fetching.set(true);
    this.outcome.set(null);
    this.pending.set(null);
    try {
      const profile = await firstValueFrom(
        this.weather.getApiWeatherWindProfile({
          latitude: at.latitude,
          longitude: at.longitude,
          hourUtc: this.hourUtc() ?? undefined,
          model: this.model(),
        }),
      );
      if (this.coordinates() !== at) return; // the pilot moved the launch location meanwhile
      if (this.hasOwnRows(rows)) this.pending.set(profile);
      else this.apply(profile, rows, createRow);
    } catch (e) {
      if (this.coordinates() === at) this.outcome.set({ kind: 'failed', message: this.describe(e) });
    } finally {
      this.fetching.set(false);
    }
  }

  confirm(rows: WindArray, createRow: () => FormGroup): void {
    const profile = this.pending();
    if (profile) this.apply(profile, rows, createRow);
  }

  cancel(): void {
    this.pending.set(null);
    this.outcome.set({ kind: 'ok', message: 'Kept your rows. The fetched wind was not applied.' });
  }

  /** True when a row has been typed or edited by the pilot: not all blank, and not just what the last fetch wrote. */
  private hasOwnRows(rows: WindArray): boolean {
    const values = rows.getRawValue() as RowValue[];
    const blank = values.every((v) => !v.altitudeM && !v.directionDeg && !v.speedKt && !v.temperatureC);
    if (blank) return false;
    return JSON.stringify(values) !== JSON.stringify(this.lastFetched);
  }

  private apply(profile: WindProfileDto, rows: WindArray, createRow: () => FormGroup): void {
    while (rows.length < profile.levels.length) rows.push(createRow());
    while (rows.length > profile.levels.length) rows.removeAt(rows.length - 1);
    const values: RowValue[] = profile.levels.map((l: WindProfileLevelDto) => ({
      altitudeM: Number(l.altitudeM),
      directionDeg: Number(l.directionDeg),
      speedKt: Number(l.speedKt),
      temperatureC: Number(l.temperatureC),
    }));
    rows.setValue(values);
    rows.markAsDirty();
    this.lastFetched = values;
    this.pending.set(null);
    this.outcome.set({ kind: 'ok', message: `Fetched ${values.length} levels (${profile.model}). You can edit them.` });
  }

  private describe(e: unknown): string {
    if (e instanceof ApiError) {
      if (e.status === 422) {
        const errors = (e.body as { errors?: Record<string, string[]> } | undefined)?.errors;
        const detail = errors ? Object.values(errors).flat().join(' ') : '';
        return `${detail || 'This model has no wind data for that place and hour.'} Your rows are unchanged.`;
      }
      if (e.status === 502) return "The forecast service can't be reached right now. Your rows are unchanged; try again, or type them in.";
    }
    return "Couldn't fetch the wind by altitude right now. Your rows are unchanged; try again, or type them in.";
  }
}
