import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { Button } from '../../../../components/button/button';
import { WeatherService } from '../../../../core/api-client';

import { createWindLevelGroup, type MeteoFormGroup } from '../../../../core/flight-preparations/flight-preparation-form';
import { FieldErrorPipe } from '../field-error.pipe';

export const NO_LOCATION_MESSAGE = 'Pick a launch location in section 01 to fetch its METAR and TAF.';
export const NO_ICAO_MESSAGE =
  "This launch location has no ICAO code, so its METAR and TAF can't be fetched. Add one under Settings → Launch locations, or type them in.";

/** How the last fetch went; `failed` is the only one shown as a problem. */
interface FetchOutcome {
  kind: 'ok' | 'partial' | 'none' | 'failed';
  message: string;
}

/**
 * Section 02 — wind/visibility/METAR-TAF plus the wind-on-altitude table. One action fetches the METAR and
 * the TAF of the launch location's ICAO station (issue #44). It only ever fills fields: a failed fetch, a
 * station with nothing published, or a part the station does not publish leaves what the pilot has untouched.
 */
@Component({
  selector: 'Nimbus-meteo-section',
  imports: [ReactiveFormsModule, FieldErrorPipe, Button],
  templateUrl: './meteo-section.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['./meteo-section.css', '../section-fields.css'],
})
export class MeteoSection {
  group = input.required<MeteoFormGroup>();
  /** ICAO code of the selected launch location, or null when it has none (or none is picked). */
  icaoCode = input<string | null>(null);
  /** Whether a launch location is picked at all; only changes which reason is given when there is no code. */
  locationChosen = input(false);

  private readonly fb = inject(NonNullableFormBuilder);
  private readonly weather = inject(WeatherService);

  protected readonly fetching = signal(false);
  protected readonly outcome = signal<FetchOutcome | null>(null);
  /** Why fetching is not possible right now (shown next to the button), or null when it is. */
  protected readonly blockedReason = computed(() => (this.icaoCode() ? null : this.locationChosen() ? NO_ICAO_MESSAGE : NO_LOCATION_MESSAGE));

  constructor() {
    // A result belongs to the station it was fetched for: picking another location starts afresh.
    effect(() => {
      this.icaoCode();
      untracked(() => this.outcome.set(null));
    });
  }

  protected get windLevels() {
    return this.group().controls.windLevels;
  }

  protected async fetchReports(): Promise<void> {
    const icao = this.icaoCode();
    if (!icao || this.fetching()) return;

    this.fetching.set(true);
    this.outcome.set(null);
    try {
      const report = await firstValueFrom(this.weather.getApiWeatherMetarTaf({ icao }));
      // The pilot switched location while this was on its way: this report is for the wrong station.
      if (this.icaoCode() !== icao) return;

      const group = this.group();
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
      if (this.icaoCode() === icao) {
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

  protected addWindLevel(): void {
    this.windLevels.push(createWindLevelGroup(this.fb));
  }

  protected removeWindLevel(index: number): void {
    this.windLevels.removeAt(index);
  }
}
