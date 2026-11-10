import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder } from '@angular/forms';
import { of } from 'rxjs';
import { vi } from 'vitest';

import { MeteoSection } from './meteo-section';
import { WeatherService } from '../../../../core/api-client';
import { createFlightPreparationForm } from '../../../../core/flight-preparations/flight-preparation-form';
import { MetarTafFetcher, NO_ICAO_MESSAGE, NO_LOCATION_MESSAGE } from '../../../../core/flight-preparations/metar-taf-fetcher';

@Component({ imports: [MeteoSection], template: `<Nimbus-meteo-section [group]="form.controls.meteo" />` })
class Host {
  form = createFlightPreparationForm(TestBed.inject(NonNullableFormBuilder));
}

describe('MeteoSection fetch result (issue #44)', () => {
  async function setup(response = { station: 'EBAW', metar: 'EBAW 100850Z 08003KT 9999 Q1013', taf: null as string | null }) {
    await TestBed.configureTestingModule({
      providers: [MetarTafFetcher, { provide: WeatherService, useValue: { getApiWeatherMetarTaf: vi.fn().mockReturnValue(of(response)) } }],
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
