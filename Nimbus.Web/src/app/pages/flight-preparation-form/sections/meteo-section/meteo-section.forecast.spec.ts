import { ChangeDetectorRef, Component, inject } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder } from '@angular/forms';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';

import { MeteoSection } from './meteo-section';
import { ApiError, WeatherService, type HourlyForecastDto } from '../../../../core/api-client';
import { createFlightPreparationForm } from '../../../../core/flight-preparations/flight-preparation-form';
import { FORECAST_UNAVAILABLE_MESSAGE, plannedLocalHour } from '../../../../core/flight-preparations/hourly-forecast-fetcher';
import { MetarTafFetcher } from '../../../../core/flight-preparations/metar-taf-fetcher';

@Component({
  imports: [MeteoSection],
  template: `<Nimbus-meteo-section
    [group]="form.controls.meteo"
    [coordinates]="coordinates"
    [plannedHour]="plannedHour"
  />`,
})
class Host {
  form = createFlightPreparationForm(TestBed.inject(NonNullableFormBuilder));
  coordinates: { latitude: number; longitude: number } | null = { latitude: 51.1568, longitude: 4.0439 };
  plannedHour: string | null = '2026-11-10T10';
  cdr = inject(ChangeDetectorRef);
}

const hour = (time: string, model: string, over: Partial<HourlyForecastDto['hours'][number]> = {}) => ({
  time,
  model,
  temperatureC: 11.2,
  windSpeedKt: 8.5,
  windDirectionDeg: 240,
  windGustKt: 14,
  capeJPerKg: 80,
  precipitationMm: 0.2,
  precipitationProbabilityPct: 30,
  ...over,
});

const forecast: HourlyForecastDto = {
  model: 'icon_seamless',
  timeZone: 'Europe/Brussels',
  utcOffsetSeconds: 3600,
  days: [
    { date: '2026-11-10', sunrise: '07:32', sunset: '16:51' },
    { date: '2026-11-11', sunrise: '07:34', sunset: '16:49' },
  ],
  hours: [
    hour('2026-11-10T09:00', 'icon_d2'),
    hour('2026-11-10T10:00', 'icon_d2', { windSpeedKt: 6, capeJPerKg: null }),
    hour('2026-11-11T09:00', 'icon_eu'),
  ],
};

describe('MeteoSection hourly forecast (issue #45)', () => {
  async function setup(api: ReturnType<typeof vi.fn>) {
    await TestBed.configureTestingModule({
      providers: [MetarTafFetcher, { provide: WeatherService, useValue: { getApiWeatherHourlyForecast: api } }],
    }).compileComponents();
    const fixture = TestBed.createComponent(Host);
    const flush = async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();
    };
    await flush();
    const el = fixture.nativeElement as HTMLElement;
    const q = (id: string) => el.querySelector(`[data-testid="${id}"]`) as HTMLElement | null;
    return { host: fixture.componentInstance, flush, q, el };
  }

  it('fetches for the launch location with the chosen model and shows the hours by day with sunrise and sunset', async () => {
    const api = vi.fn().mockReturnValue(of(forecast));
    const { flush, q } = await setup(api);

    q('forecast-model')!.dispatchEvent(Object.assign(new Event('change'), {}));
    q('fetch-forecast')!.click();
    await flush();

    expect(api).toHaveBeenCalledWith({ latitude: 51.1568, longitude: 4.0439, model: 'icon_seamless' });
    expect(q('forecast-day-2026-11-10')!.textContent).toContain('07:32 – 16:51');
    expect(q('forecast-day-2026-11-11')!.textContent).toContain('07:34 – 16:49');
    expect(q('forecast-hour-2026-11-10T09:00')!.textContent).toContain('09:00');
    expect(q('forecast-summary')!.textContent).toContain('Europe/Brussels');
  });

  it("highlights the hour of the planned flight, in the site's local time", async () => {
    const { flush, q } = await setup(vi.fn().mockReturnValue(of(forecast)));
    q('fetch-forecast')!.click();
    await flush();

    expect(q('forecast-hour-2026-11-10T10:00')!.classList).toContain('planned');
    expect(q('forecast-hour-2026-11-10T10:00')!.getAttribute('aria-current')).toBe('time');
    expect(q('forecast-hour-2026-11-10T09:00')!.classList).not.toContain('planned');
  });

  it('marks where the model changes, so the resolution drop is visible', async () => {
    const { flush, q, el } = await setup(vi.fn().mockReturnValue(of(forecast)));
    q('fetch-forecast')!.click();
    await flush();

    const changes = el.querySelectorAll('[data-testid="forecast-model-change"]');
    expect(changes).toHaveLength(1);
    expect(changes[0].textContent).toContain('icon_eu from here (coarser than icon_d2)');
  });

  it('shows a value the model lacks as a dash, never as zero', async () => {
    const { flush, q } = await setup(vi.fn().mockReturnValue(of(forecast)));
    q('fetch-forecast')!.click();
    await flush();

    expect(q('forecast-hour-2026-11-10T10:00')!.querySelectorAll('td')[5].textContent?.trim()).toBe('–');
  });

  it('fills wind, direction, CAPE and temperature from the chosen hour, leaves a missing value alone', async () => {
    const { host, flush, q } = await setup(vi.fn().mockReturnValue(of(forecast)));
    host.form.controls.meteo.controls.capeJPerKg.setValue(123);
    q('fetch-forecast')!.click();
    await flush();

    q('forecast-use-2026-11-10T10:00')!.click();
    await flush();

    const m = host.form.controls.meteo.controls;
    expect(m.windSpeedKt.value).toBe(6);
    expect(m.windDirectionDeg.value).toBe(240);
    expect(m.temperatureC.value).toBe(11.2);
    expect(m.capeJPerKg.value).toBe(123);
    expect(m.windSpeedKt.dirty).toBe(true);
  });

  it('renders an empty state, not an error, when the provider is unavailable, and changes nothing', async () => {
    const api = vi.fn().mockReturnValue(throwError(() => new ApiError({ method: 'GET', url: '' }, { url: '', ok: false, status: 502, statusText: 'Bad Gateway', body: {} }, 'x')));
    const { host, flush, q } = await setup(api);
    host.form.controls.meteo.controls.windSpeedKt.setValue(9);
    q('fetch-forecast')!.click();
    await flush();

    expect(q('forecast-empty')!.textContent).toContain(FORECAST_UNAVAILABLE_MESSAGE);
    expect(q('forecast-hour-2026-11-10T09:00')).toBeNull();
    expect(host.form.controls.meteo.controls.windSpeedKt.value).toBe(9);
  });

  it('shows an empty state when the model returns no hours', async () => {
    const { flush, q } = await setup(vi.fn().mockReturnValue(of({ ...forecast, hours: [] })));
    q('fetch-forecast')!.click();
    await flush();

    expect(q('forecast-empty')).not.toBeNull();
  });

  it('cannot fetch without a launch location, and says why', async () => {
    const { host, flush, q } = await setup(vi.fn());
    host.coordinates = null;
    host.cdr.markForCheck();
    await flush();

    expect(q('forecast-blocked')).not.toBeNull();
    expect((q('fetch-forecast') as HTMLButtonElement).disabled).toBe(true);
  });

  it('builds the planned hour from the form date and time as typed', () => {
    expect(plannedLocalHour('2026-11-10', '10:45')).toBe('2026-11-10T10');
    expect(plannedLocalHour('', '10:45')).toBeNull();
    expect(plannedLocalHour('2026-11-10', '')).toBeNull();
  });
});
