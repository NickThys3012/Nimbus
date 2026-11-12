import { FlightImagesApi } from '../../../../core/flight-preparations/flight-images.api';
import { WindProfileFetcher } from '../../../../core/flight-preparations/wind-profile-fetcher';
import { ChangeDetectorRef, Component, inject } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';

import { MeteoSection } from './meteo-section';
import { ApiError, WeatherService } from '../../../../core/api-client';
import { createFlightPreparationForm } from '../../../../core/flight-preparations/flight-preparation-form';
import { WIND_PROFILE_MODELS } from '../../../../core/flight-preparations/wind-profile-fetcher';
import { MetarTafFetcher, NO_ICAO_MESSAGE, NO_LOCATION_MESSAGE } from '../../../../core/flight-preparations/metar-taf-fetcher';

@Component({ imports: [MeteoSection], template: `<Nimbus-meteo-section [group]="form.controls.meteo" />` })
class Host {
  form = createFlightPreparationForm(TestBed.inject(NonNullableFormBuilder));
}

@Component({
  imports: [MeteoSection],
  template: `<Nimbus-meteo-section [group]="form.controls.meteo" [coordinates]="coordinates" />`,
})
class WindHost {
  form = createFlightPreparationForm(TestBed.inject(NonNullableFormBuilder));
  coordinates: { latitude: number; longitude: number } | null = null;
  cdr = inject(ChangeDetectorRef);
}

describe('MeteoSection fetch result (issue #44)', () => {
  async function setup(response = { station: 'EBAW', metar: 'EBAW 100850Z 08003KT 9999 Q1013', taf: null as string | null }) {
    await TestBed.configureTestingModule({
      providers: [MetarTafFetcher, WindProfileFetcher, { provide: FlightImagesApi, useValue: { list: () => of([]) } }, { provide: WeatherService, useValue: { getApiWeatherMetarTaf: vi.fn().mockReturnValue(of(response)) } }],
    }).compileComponents();
    const fetcher = TestBed.inject(MetarTafFetcher);
    const fixture = TestBed.createComponent(Host);
    const flush = async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();
    };
    await flush();
    const el = fixture.nativeElement as HTMLElement;
    return { fetcher, host: fixture.componentInstance, flush, q: (id: string) => el.querySelector(`[data-testid="${id}"]`) as HTMLElement | null, el };
  }

  it('has no Fetch button of its own: the action lives in the section header', async () => {
    const { el } = await setup();
    expect(el.querySelector('[data-testid="fetch-metar-taf"]')).toBeNull();
  });

  it('shows nothing before a fetch when a station is available', async () => {
    const { fetcher, flush, q, el } = await setup();
    fetcher.station.set({ chosen: true, icaoCode: 'EBAW' });
    await flush();

    expect(q('fetch-blocked')).toBeNull();
    expect(el.querySelector('.fetch-note')?.textContent?.trim()).toBe('');
  });

  it('explains why a fetch is not possible, with a different reason for no location and for no ICAO code', async () => {
    const { fetcher, flush, q } = await setup();

    fetcher.station.set({ chosen: false, icaoCode: null });
    await flush();
    expect(q('fetch-blocked')?.textContent).toBe(NO_LOCATION_MESSAGE);

    fetcher.station.set({ chosen: true, icaoCode: null });
    await flush();
    expect(q('fetch-blocked')?.textContent).toBe(NO_ICAO_MESSAGE);
  });

  it('shows how the last fetch went, a failure as a problem', async () => {
    const { fetcher, host, flush, q } = await setup();
    fetcher.station.set({ chosen: true, icaoCode: 'EBAW' });
    TestBed.tick();

    await fetcher.fetch(host.form.controls.meteo);
    await flush();
    expect(q('fetch-partial')?.textContent).toContain('No TAF is published');
    expect(q('fetch-partial')?.className).not.toContain('problem');

    fetcher.outcome.set({ kind: 'failed', message: "Couldn't fetch the METAR and TAF right now. Your entries are unchanged; try again, or type them in." });
    await flush();
    expect(q('fetch-failed')?.className).toContain('problem');
  });
});

describe('MeteoSection wind by altitude (issue #46)', () => {
  const profile = {
    model: 'icon_seamless',
    forecastHourUtc: '2026-11-10T09:00:00Z',
    levels: [
      { altitudeM: 10, directionDeg: 250, speedKt: 6, temperatureC: 12, altitudeSource: 'agl' },
      { altitudeM: 540, directionDeg: 260, speedKt: 11, temperatureC: 9, altitudeSource: 'geopotential' },
    ],
  };

  async function setup(result: unknown = of(profile)) {
    const api = { getApiWeatherMetarTaf: vi.fn(), getApiWeatherWindProfile: vi.fn().mockReturnValue(result) };
    await TestBed.configureTestingModule({ providers: [MetarTafFetcher, WindProfileFetcher, { provide: FlightImagesApi, useValue: { list: () => of([]) } }, { provide: WeatherService, useValue: api }] }).compileComponents();
    const fixture = TestBed.createComponent(WindHost);
    fixture.componentInstance.coordinates = { latitude: 51.1568, longitude: 4.0439 };
    const flush = async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();
    };
    await flush();
    const el = fixture.nativeElement as HTMLElement;
    const q = (id: string) => el.querySelector(`[data-testid="${id}"]`) as HTMLElement | null;
    const rows = () => fixture.componentInstance.form.controls.meteo.controls.windLevels;
    return { api, host: fixture.componentInstance, flush, q, rows };
  }

  it('offers only the supported models, icon_seamless first and selected', async () => {
    const { q } = await setup();
    const select = q('wind-model') as HTMLSelectElement;
    expect(select.value).toBe('icon_seamless');
    const options = Array.from(select.options).map((o) => o.value);
    expect(options).toEqual([...WIND_PROFILE_MODELS]);
    expect(options).not.toContain('ecmwf_ifs');
  });

  it('fills the empty table from the fetch, sending the chosen model and the launch location', async () => {
    const { api, flush, q, rows } = await setup();
    const select = q('wind-model') as HTMLSelectElement;
    select.value = 'icon_d2';
    select.dispatchEvent(new Event('change'));
    q('fetch-wind-profile')!.click();
    await flush();

    expect(api.getApiWeatherWindProfile).toHaveBeenCalledWith({ latitude: 51.1568, longitude: 4.0439, hourUtc: undefined, model: 'icon_d2' });
    expect(rows().getRawValue()).toEqual([
      { altitudeM: 10, directionDeg: 250, speedKt: 6, temperatureC: 12 },
      { altitudeM: 540, directionDeg: 260, speedKt: 11, temperatureC: 9 },
    ]);
    expect(q('wind-ok')?.textContent).toContain('Fetched 2 levels');
    expect(q('wind-overwrite-dialog')?.querySelector('[role="alertdialog"]') ?? null).toBeNull();
  });

  it('keeps + Level working after a fetch', async () => {
    const { flush, q, rows } = await setup();
    q('fetch-wind-profile')!.click();
    await flush();
    q('add-wind-level')!.click();
    await flush();
    expect(rows().length).toBe(3);
  });

  it('asks before overwriting rows the pilot edited, and keeps them on "Keep mine"', async () => {
    const { flush, q, rows } = await setup();
    rows().at(0).patchValue({ altitudeM: 300, directionDeg: 245, speedKt: 9, temperatureC: 17 });
    q('fetch-wind-profile')!.click();
    await flush();

    expect(q('wind-overwrite-dialog')?.querySelector('[role="alertdialog"]') ?? null).not.toBeNull();
    expect(rows().length).toBe(1);
    expect(rows().at(0).getRawValue().altitudeM).toBe(300);

    q('wind-overwrite-cancel')!.click();
    await flush();
    expect(q('wind-overwrite-dialog')?.querySelector('[role="alertdialog"]') ?? null).toBeNull();
    expect(rows().length).toBe(1);
    expect(rows().at(0).getRawValue().altitudeM).toBe(300);
  });

  it('replaces the pilot rows only after "Replace"', async () => {
    const { flush, q, rows } = await setup();
    rows().at(0).patchValue({ altitudeM: 300, speedKt: 9 });
    q('fetch-wind-profile')!.click();
    await flush();
    q('wind-overwrite-confirm')!.click();
    await flush();

    expect(q('wind-overwrite-dialog')?.querySelector('[role="alertdialog"]') ?? null).toBeNull();
    expect(rows().length).toBe(2);
    expect(rows().at(1).getRawValue().altitudeM).toBe(540);
  });

  it('does not ask when the rows are still exactly what the last fetch wrote, but asks once the pilot edits one', async () => {
    const { flush, q, rows } = await setup();
    q('fetch-wind-profile')!.click();
    await flush();
    q('fetch-wind-profile')!.click();
    await flush();
    expect(q('wind-overwrite-dialog')?.querySelector('[role="alertdialog"]') ?? null).toBeNull();

    rows().at(1).patchValue({ speedKt: 99 });
    q('fetch-wind-profile')!.click();
    await flush();
    expect(q('wind-overwrite-dialog')?.querySelector('[role="alertdialog"]') ?? null).not.toBeNull();
  });

  it('explains a 422 (model lacks levels) with the server message and changes nothing', async () => {
    const err = new ApiError({ method: 'GET', url: '' }, { url: '', ok: false, status: 422, statusText: '', body: {} }, 'x');
    (err as { body: unknown }).body = { errors: { Model: ["Model 'icon_d2' returned no data for: 950 hPa at that place and hour."] } };
    const { flush, q, rows } = await setup(throwError(() => err));
    q('fetch-wind-profile')!.click();
    await flush();

    expect(q('wind-failed')?.textContent).toContain('950 hPa');
    expect(q('wind-failed')?.textContent).toContain('unchanged');
    expect(rows().length).toBe(1);
  });

  it('explains a 502 (provider down) and changes nothing', async () => {
    const err = new ApiError({ method: 'GET', url: '' }, { url: '', ok: false, status: 502, statusText: '', body: {} }, 'x');
    const { flush, q, rows } = await setup(throwError(() => err));
    rows().at(0).patchValue({ altitudeM: 300 });
    q('fetch-wind-profile')!.click();
    await flush();

    expect(q('wind-failed')?.textContent).toContain("can't be reached");
    expect(q('wind-overwrite-dialog')?.querySelector('[role="alertdialog"]') ?? null).toBeNull();
    expect(rows().at(0).getRawValue().altitudeM).toBe(300);
  });

  it('is blocked, with the reason, without a launch location', async () => {
    const { host, flush, q } = await setup();
    host.coordinates = null;
    host.cdr.markForCheck();
    await flush();
    expect(q('wind-blocked')?.textContent).toContain('launch location');
    expect((q('fetch-wind-profile') as HTMLButtonElement).disabled).toBe(true);
  });
});

@Component({ imports: [MeteoSection], template: `<Nimbus-meteo-section [group]="form.controls.meteo" flightId="f1" />` })
class ImagesHost {
  form = createFlightPreparationForm(TestBed.inject(NonNullableFormBuilder));
}

describe('MeteoSection images and precipitation (issue #50)', () => {
  it('has a precipitation field in the form', async () => {
    await TestBed.configureTestingModule({
      providers: [MetarTafFetcher, WindProfileFetcher, { provide: FlightImagesApi, useValue: { list: () => of([]) } }, { provide: WeatherService, useValue: {} }],
    }).compileComponents();
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const input = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="precipitation"]') as HTMLInputElement;
    input.value = 'light rain after 19:00';
    input.dispatchEvent(new Event('input'));
    expect(fixture.componentInstance.form.controls.meteo.controls.precipitation.value).toBe('light rain after 19:00');
  });

  it('a failed image upload shows its error and leaves everything typed in the form exactly as it was', async () => {
    const upload = vi.fn().mockReturnValue(throwError(() => new HttpErrorResponse({ status: 502 })));
    await TestBed.configureTestingModule({
      providers: [
        MetarTafFetcher,
        WindProfileFetcher,
        { provide: FlightImagesApi, useValue: { list: () => of([]), upload } },
        { provide: WeatherService, useValue: {} },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(ImagesHost);
    fixture.detectChanges();
    await fixture.whenStable();
    const meteo = fixture.componentInstance.form.controls.meteo;
    meteo.patchValue({ windSpeedKt: 14, metar: 'EBAW 081750Z 24006KT', precipitation: 'none' });
    meteo.markAsDirty();
    const before = JSON.stringify(meteo.getRawValue());

    const el = fixture.nativeElement as HTMLElement;
    const input = el.querySelector('[data-testid="image-file"]') as HTMLInputElement;
    Object.defineProperty(input, 'files', { value: [new File([new Uint8Array(10)], 'stuve.png', { type: 'image/png' })], configurable: true });
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(upload).toHaveBeenCalled();
    expect(el.querySelector('[data-testid="upload-error"]')?.textContent).toContain('file store is unavailable');
    expect(JSON.stringify(meteo.getRawValue())).toBe(before);
    expect(meteo.dirty).toBe(true);
  });
});
