import { computed, effect, inject, Injectable, signal, untracked } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ApiError, WeatherService, type ForecastDayDto, type HourlyForecastDto, type HourlyForecastHourDto } from '../api-client';

/** Models the pilot can pick for the hourly forecast (mirrors the API's list, issue #45). */
export const FORECAST_MODELS = [
  'icon_seamless',
  'icon_d2',
  'icon_eu',
  'icon_global',
  'meteofrance_seamless',
  'meteofrance_arome_france',
  'meteofrance_arpege_europe',
  'meteofrance_arpege_world',
  'cma_grapes_global',
  'ukmo_seamless',
  'ukmo_uk_deterministic_2km',
  'ukmo_global_deterministic_10km',
  'best_match',
  'knmi_seamless',
  'knmi_harmonie_arome_europe',
  'dmi_seamless',
  'dmi_harmonie_arome_europe',
  'metno_seamless',
  'ecmwf_ifs',
  'ecmwf_ifs025',
  'ecmwf_aifs025_single',
  'gfs_seamless',
  'gfs_global',
  'gem_seamless',
  'gem_global',
  'gem_regional',
  'jma_seamless',
  'jma_gsm',
] as const;
export const DEFAULT_FORECAST_MODEL = 'icon_seamless';

export const NO_FORECAST_COORDINATES_MESSAGE = 'Pick a launch location in section 01 to fetch the hourly forecast.';
export const FORECAST_UNAVAILABLE_MESSAGE = "The forecast isn't available right now. The rest of the form is unaffected; try again later.";

/** One day of the forecast: its sunrise and sunset and its hours, each marked when its model differs from the hour before. */
export interface ForecastDayGroup {
  date: string;
  sunrise: string | null;
  sunset: string | null;
  hours: ForecastRow[];
}
export interface ForecastRow {
  hour: HourlyForecastHourDto;
  /** Set on the first hour of a model after another one: the resolution changes from here. */
  modelChange: { from: string; to: string } | null;
}

/**
 * Fetches the hourly forecast for the launch location (issue #45). Read-only: nothing is stored. A provider that is down
 * (or has nothing for the place) is an empty state with a reason, never an error page, and the form is unaffected.
 */
@Injectable()
export class HourlyForecastFetcher {
  private readonly weather = inject(WeatherService);

  readonly coordinates = signal<{ latitude: number; longitude: number } | null>(null);
  readonly model = signal<string>(DEFAULT_FORECAST_MODEL);
  readonly fetching = signal(false);
  readonly forecast = signal<HourlyForecastDto | null>(null);
  /** Why there is no forecast to show: set after a failed or empty fetch. */
  readonly unavailable = signal<string | null>(null);

  readonly blockedReason = computed(() => (this.coordinates() ? null : NO_FORECAST_COORDINATES_MESSAGE));

  /** The forecast grouped by local day, with sunrise/sunset on each day and the resolution drops marked. */
  readonly days = computed<ForecastDayGroup[]>(() => {
    const f = this.forecast();
    if (!f) return [];
    const byDate = new Map<string, ForecastDayDto>(f.days.map((d) => [d.date, d]));
    const groups: ForecastDayGroup[] = [];
    let previous: HourlyForecastHourDto | null = null;
    for (const hour of f.hours) {
      const date = hour.time.slice(0, 10);
      let group = groups.find((g) => g.date === date);
      if (!group) {
        const day = byDate.get(date);
        group = { date, sunrise: day?.sunrise ?? null, sunset: day?.sunset ?? null, hours: [] };
        groups.push(group);
      }
      const modelChange = previous && previous.model !== hour.model ? { from: previous.model, to: hour.model } : null;
      group.hours.push({ hour, modelChange });
      previous = hour;
    }
    return groups;
  });

  constructor() {
    // A forecast belongs to the place it was fetched for.
    effect(() => {
      this.coordinates();
      untracked(() => {
        this.forecast.set(null);
        this.unavailable.set(null);
      });
    });
  }

  async fetch(): Promise<void> {
    const at = this.coordinates();
    if (!at || this.fetching()) return;

    this.fetching.set(true);
    this.unavailable.set(null);
    try {
      const result = await firstValueFrom(
        this.weather.getApiWeatherHourlyForecast({ latitude: at.latitude, longitude: at.longitude, model: this.model() }),
      );
      if (this.coordinates() !== at) return; // the pilot moved the launch location meanwhile
      if (result.hours.length === 0) {
        this.forecast.set(null);
        this.unavailable.set('This model has no forecast for that place. Choose another model.');
      } else {
        this.forecast.set(result);
      }
    } catch (e) {
      if (this.coordinates() === at) {
        this.forecast.set(null);
        this.unavailable.set(e instanceof ApiError && e.status === 422 ? this.detail(e) : FORECAST_UNAVAILABLE_MESSAGE);
      }
    } finally {
      this.fetching.set(false);
    }
  }

  private detail(e: ApiError): string {
    const errors = (e.body as { errors?: Record<string, string[]> } | undefined)?.errors;
    return (errors ? Object.values(errors).flat().join(' ') : '') || FORECAST_UNAVAILABLE_MESSAGE;
  }
}

/** The planned flight's local hour (`yyyy-MM-ddTHH`), as typed in the form: the forecast hours are in the same site-local time. */
export function plannedLocalHour(date: string, time: string): string | null {
  return date && /^\d{2}:\d{2}/.test(time) ? `${date}T${time.slice(0, 2)}` : null;
}

export function isPlannedHour(hour: HourlyForecastHourDto, planned: string | null): boolean {
  return planned !== null && hour.time.startsWith(planned);
}
