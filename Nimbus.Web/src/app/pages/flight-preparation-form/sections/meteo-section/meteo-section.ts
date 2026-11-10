import { ChangeDetectionStrategy, Component, effect, inject, input, output } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';

import type { HourlyForecastHourDto } from '../../../../core/api-client';
import {
  DEFAULT_FORECAST_MODEL,
  FORECAST_MODELS,
  HourlyForecastFetcher,
  isPlannedHour,
} from '../../../../core/flight-preparations/hourly-forecast-fetcher';
import { createWindLevelGroup, type MeteoFormGroup } from '../../../../core/flight-preparations/flight-preparation-form';
import { MetarTafFetcher } from '../../../../core/flight-preparations/metar-taf-fetcher';
import { DEFAULT_WIND_PROFILE_MODEL, WIND_PROFILE_MODELS, WindProfileFetcher } from '../../../../core/flight-preparations/wind-profile-fetcher';
import { Button } from '../../../../components/button/button';
import { Modal } from '../../../../components/modal/modal';
import { FieldErrorPipe } from '../field-error.pipe';

/**
 * Section 02 — wind/visibility/METAR-TAF plus the wind-on-altitude table. The Fetch action is in this section's header
 * (the page owns it, see `MetarTafFetcher`); here the section shows how the last fetch went, or why none is possible.
 */
@Component({
  selector: 'Nimbus-meteo-section',
  imports: [ReactiveFormsModule, FieldErrorPipe, Button, Modal],
  providers: [WindProfileFetcher, HourlyForecastFetcher],
  templateUrl: './meteo-section.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['./meteo-section.css', '../section-fields.css'],
})
export class MeteoSection {
  group = input.required<MeteoFormGroup>();
  /** The launch location's coordinates, for the wind-by-altitude fetch; null until a location is picked. */
  coordinates = input<{ latitude: number; longitude: number } | null>(null);
  /** The flight's take-off hour (UTC ISO) so the wind is for that hour; null means the current hour. */
  hourUtc = input<string | null>(null);
  /** The planned flight's local hour (`yyyy-MM-ddTHH`) so the forecast can highlight it; null until date and time are filled in. */
  plannedHour = input<string | null>(null);
  /** The pilot picked a forecast hour: the page fills what lives outside this section (sunrise and sunset of that day). */
  hourUsed = output<{ sunrise: string | null; sunset: string | null }>();
  private readonly fb = inject(NonNullableFormBuilder);
  protected readonly fetcher = inject(MetarTafFetcher);
  protected readonly wind = inject(WindProfileFetcher);
  protected readonly models = WIND_PROFILE_MODELS;
  protected readonly defaultModel = DEFAULT_WIND_PROFILE_MODEL;
  protected readonly forecast = inject(HourlyForecastFetcher);
  protected readonly forecastModels = FORECAST_MODELS;
  protected readonly defaultForecastModel = DEFAULT_FORECAST_MODEL;

  constructor() {
    effect(() => this.wind.coordinates.set(this.coordinates()));
    effect(() => this.wind.hourUtc.set(this.hourUtc()));
    effect(() => this.forecast.coordinates.set(this.coordinates()));
  }

  protected isPlanned(hour: HourlyForecastHourDto): boolean {
    return isPlannedHour(hour, this.plannedHour());
  }

  protected fetchForecast(): void {
    void this.forecast.fetch();
  }

  protected setForecastModel(event: Event): void {
    this.forecast.model.set((event.target as HTMLSelectElement).value);
  }

  /** Fills wind, direction, CAPE and temperature from the chosen hour (a value the model lacks leaves its field alone). */
  protected useHour(hour: HourlyForecastHourDto, day: { sunrise: string | null; sunset: string | null }): void {
    const c = this.group().controls;
    const fill = (control: { setValue(v: number): void; markAsDirty(): void }, value: number | string | null | undefined) => {
      if (value === null || value === undefined) return;
      control.setValue(Number(value));
      control.markAsDirty();
    };
    fill(c.windSpeedKt, hour.windSpeedKt);
    fill(c.windDirectionDeg, hour.windDirectionDeg);
    fill(c.capeJPerKg, hour.capeJPerKg);
    fill(c.temperatureC, hour.temperatureC);
    this.hourUsed.emit({ sunrise: day.sunrise, sunset: day.sunset });
  }

  private readonly createRow = () => createWindLevelGroup(this.fb);

  protected fetchWind(): void {
    void this.wind.fetch(this.windLevels, this.createRow);
  }

  protected confirmWind(): void {
    this.wind.confirm(this.windLevels, this.createRow);
  }

  protected setModel(event: Event): void {
    this.wind.model.set((event.target as HTMLSelectElement).value);
  }

  protected get windLevels() {
    return this.group().controls.windLevels;
  }

  protected addWindLevel(): void {
    this.windLevels.push(createWindLevelGroup(this.fb));
  }

  protected removeWindLevel(index: number): void {
    this.windLevels.removeAt(index);
  }
}
