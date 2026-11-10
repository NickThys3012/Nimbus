import { computed, effect, inject, Injectable, signal, untracked } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { WeatherService } from '../api-client';
import type { MeteoFormGroup } from './flight-preparation-form';

export const NO_LOCATION_MESSAGE = 'Pick a launch location in section 01 to fetch its METAR and TAF.';
export const NO_ICAO_MESSAGE =
  "This launch location has no ICAO code, so its METAR and TAF can't be fetched. Add one under Settings → Launch locations, or type them in.";

/** How the last fetch went; `failed` is the only one shown as a problem. */
export interface FetchOutcome {
  kind: 'ok' | 'partial' | 'none' | 'failed';
  message: string;
}

/**
 * Fetches the METAR and TAF of the launch location's ICAO station into the meteo fields (issue #44). It lives for the
 * length of the flight form (the page provides it) because the Fetch button is in the Meteo section's header, which
 * is there whether the section is open or not, while the result is shown in the section.
 *
 * It only ever fills fields: a failed fetch, a station with nothing published, or a part the station does not publish
 * leaves what the pilot has untouched.
 */
@Injectable()
export class MetarTafFetcher {
  private readonly weather = inject(WeatherService);

  /** The launch location the form currently has; the page keeps this up to date. */
  readonly station = signal<{ chosen: boolean; icaoCode: string | null }>({ chosen: false, icaoCode: null });
  readonly fetching = signal(false);
  readonly outcome = signal<FetchOutcome | null>(null);

  private readonly icao = computed(() => this.station().icaoCode);
  /** Why fetching is not possible right now, or null when it is. */
  readonly blockedReason = computed(() => (this.icao() ? null : this.station().chosen ? NO_ICAO_MESSAGE : NO_LOCATION_MESSAGE));

  constructor() {
    // A result belongs to the station it was fetched for: picking another location starts afresh.
    effect(() => {
      this.icao();
      untracked(() => this.outcome.set(null));
    });
  }

  async fetch(group: MeteoFormGroup): Promise<void> {
    const icao = this.icao();
    if (!icao || this.fetching()) return;

    this.fetching.set(true);
    this.outcome.set(null);
    try {
      const report = await firstValueFrom(this.weather.getApiWeatherMetarTaf({ icao }));
      // The pilot switched location while this was on its way: this report is for the wrong station.
      if (this.icao() !== icao) return;

      if (report.metar) this.fill(group.controls.metar, report.metar);
      if (report.taf) this.fill(group.controls.taf, report.taf);

      this.outcome.set(
        report.metar && report.taf
          ? { kind: 'ok', message: `Fetched the METAR and TAF for ${report.station}.` }
          : report.metar
            ? { kind: 'partial', message: `Fetched the METAR for ${report.station}. No TAF is published for it; your TAF is unchanged.` }
            : report.taf
              ? { kind: 'partial', message: `Fetched the TAF for ${report.station}. No METAR is published for it; your METAR is unchanged.` }
              : { kind: 'none', message: `No METAR or TAF is published for ${report.station}. Your entries are unchanged.` },
      );
    } catch {
      if (this.icao() === icao) {
        this.outcome.set({ kind: 'failed', message: "Couldn't fetch the METAR and TAF right now. Your entries are unchanged; try again, or type them in." });
      }
    } finally {
      this.fetching.set(false);
    }
  }

  /** Sets a fetched report as if typed, so the section counts as edited and the draft is kept. */
  private fill(control: { setValue(value: string): void; markAsDirty(): void }, value: string): void {
    control.setValue(value);
    control.markAsDirty();
  }
}
