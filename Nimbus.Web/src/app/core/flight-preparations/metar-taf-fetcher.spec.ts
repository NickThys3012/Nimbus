import { TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder } from '@angular/forms';
import { of, Subject, throwError } from 'rxjs';
import { type Mock, vi } from 'vitest';

import { type MetarTafDto, WeatherService } from '../api-client';
import { createFlightPreparationForm } from './flight-preparation-form';
import { MetarTafFetcher, NO_ICAO_MESSAGE, NO_LOCATION_MESSAGE } from './metar-taf-fetcher';

const METAR = 'EBAW 100850Z 08003KT 0400 FG BKN001 01/01 Q1013';
const TAF = 'TAF EBAW 100800Z 1009/1018 09006KT 0500 FG BKN001\n  BECMG 1009/1011 6000 NSW SCT010';

describe('MetarTafFetcher (issue #44)', () => {
  let api: { getApiWeatherMetarTaf: Mock };

  function setup(response: MetarTafDto | Error | Subject<MetarTafDto> = { station: 'EBAW', metar: METAR, taf: TAF }) {
    api = {
      getApiWeatherMetarTaf: vi.fn().mockReturnValue(response instanceof Error ? throwError(() => response) : response instanceof Subject ? response : of(response)),
    };
    TestBed.configureTestingModule({ providers: [MetarTafFetcher, { provide: WeatherService, useValue: api }] });
    const fetcher = TestBed.inject(MetarTafFetcher);
    fetcher.station.set({ chosen: true, icaoCode: 'EBAW' });
    TestBed.tick();
    const meteo = createFlightPreparationForm(TestBed.inject(NonNullableFormBuilder)).controls.meteo;
    return { fetcher, meteo };
  }

  it('fetches for the launch location ICAO and fills both fields', async () => {
    const { fetcher, meteo } = setup();

    await fetcher.fetch(meteo);

    expect(api.getApiWeatherMetarTaf).toHaveBeenCalledWith({ icao: 'EBAW' });
    expect(meteo.controls.metar.value).toBe(METAR);
    expect(fetcher.outcome()).toEqual({ kind: 'ok', message: 'Fetched the METAR and TAF for EBAW.' });
  });

  it('keeps the TAF as its full multi-line text', async () => {
    const { fetcher, meteo } = setup();

    await fetcher.fetch(meteo);

    expect(meteo.controls.taf.value).toBe(TAF);
    expect(meteo.controls.taf.value.split('\n')[1]).toBe('  BECMG 1009/1011 6000 NSW SCT010');
  });

  it('counts as an edit of the section, so the draft is kept and the section is marked edited', async () => {
    const { fetcher, meteo } = setup();
    expect(meteo.dirty).toBe(false);

    await fetcher.fetch(meteo);

    expect([meteo.controls.metar.dirty, meteo.controls.taf.dirty, meteo.dirty]).toEqual([true, true, true]);
  });

  it('replaces what was there with the fresh report, because the pilot asked for it', async () => {
    const { fetcher, meteo } = setup();
    meteo.controls.metar.setValue('old text');

    await fetcher.fetch(meteo);

    expect(meteo.controls.metar.value).toBe(METAR);
  });

  describe('without an ICAO code', () => {
    it('is blocked with the reason when the launch location has none, and a fetch does nothing', async () => {
      const { fetcher, meteo } = setup();
      fetcher.station.set({ chosen: true, icaoCode: null });

      expect(fetcher.blockedReason()).toBe(NO_ICAO_MESSAGE);
      expect(NO_ICAO_MESSAGE).toContain('no ICAO code');
      await fetcher.fetch(meteo);
      expect(api.getApiWeatherMetarTaf).not.toHaveBeenCalled();
    });

    it('gives a different reason when no launch location is picked yet', () => {
      const { fetcher } = setup();
      fetcher.station.set({ chosen: false, icaoCode: null });

      expect(fetcher.blockedReason()).toBe(NO_LOCATION_MESSAGE);
    });

    it('is possible again as soon as a location with a code is picked', () => {
      const { fetcher } = setup();
      fetcher.station.set({ chosen: true, icaoCode: null });
      fetcher.station.set({ chosen: true, icaoCode: 'EBBR' });

      expect(fetcher.blockedReason()).toBeNull();
    });
  });

  describe('when the provider fails', () => {
    it('tells the pilot, leaves the existing values exactly as they were, and does not throw', async () => {
      const { fetcher, meteo } = setup(new Error('HTTP 502'));
      meteo.controls.metar.setValue('my own METAR');
      meteo.controls.taf.setValue('my own TAF');
      meteo.markAsPristine();

      await expect(fetcher.fetch(meteo)).resolves.toBeUndefined();

      expect([meteo.controls.metar.value, meteo.controls.taf.value]).toEqual(['my own METAR', 'my own TAF']);
      expect(meteo.dirty).toBe(false);
      expect(fetcher.outcome()?.kind).toBe('failed');
      expect(fetcher.outcome()?.message).toContain('Your entries are unchanged');
    });

    it('can be tried again, and a retry that works fills the fields', async () => {
      const { fetcher, meteo } = setup(new Error('HTTP 502'));
      await fetcher.fetch(meteo);
      expect(fetcher.outcome()?.kind).toBe('failed');

      api.getApiWeatherMetarTaf.mockReturnValue(of({ station: 'EBAW', metar: METAR, taf: TAF }));
      await fetcher.fetch(meteo);

      expect(fetcher.outcome()?.kind).toBe('ok');
      expect(meteo.controls.metar.value).toBe(METAR);
    });
  });

  describe('when the station does not publish everything', () => {
    it('fills the METAR and leaves the TAF alone when there is no TAF', async () => {
      const { fetcher, meteo } = setup({ station: 'EBOS', metar: 'EBOS 100850Z 09004KT 9999 Q1013', taf: null });
      meteo.controls.taf.setValue('my own TAF');

      await fetcher.fetch(meteo);

      expect([meteo.controls.metar.value, meteo.controls.taf.value]).toEqual(['EBOS 100850Z 09004KT 9999 Q1013', 'my own TAF']);
      expect(fetcher.outcome()).toMatchObject({ kind: 'partial' });
      expect(fetcher.outcome()?.message).toContain('No TAF is published');
    });

    it('fills the TAF and leaves the METAR alone when there is no METAR', async () => {
      const { fetcher, meteo } = setup({ station: 'EBOS', metar: null, taf: TAF });
      meteo.controls.metar.setValue('my own METAR');

      await fetcher.fetch(meteo);

      expect([meteo.controls.metar.value, meteo.controls.taf.value]).toEqual(['my own METAR', TAF]);
      expect(fetcher.outcome()?.message).toContain('No METAR is published');
    });

    it('changes nothing and says so when nothing is published at all', async () => {
      const { fetcher, meteo } = setup({ station: 'ZZZZ', metar: null, taf: null });
      meteo.controls.metar.setValue('my own METAR');
      meteo.controls.taf.setValue('my own TAF');
      meteo.markAsPristine();

      await fetcher.fetch(meteo);

      expect([meteo.controls.metar.value, meteo.controls.taf.value]).toEqual(['my own METAR', 'my own TAF']);
      expect(meteo.dirty).toBe(false);
      expect(fetcher.outcome()).toMatchObject({ kind: 'none' });
      expect(fetcher.outcome()?.message).toContain('No METAR or TAF is published for ZZZZ');
    });
  });

  it('sends one request however often it is asked while it is waiting', async () => {
    const pending = new Subject<MetarTafDto>();
    const { fetcher, meteo } = setup(pending);

    const first = fetcher.fetch(meteo);
    await fetcher.fetch(meteo);
    expect(fetcher.fetching()).toBe(true);

    pending.next({ station: 'EBAW', metar: METAR, taf: TAF });
    pending.complete();
    await first;

    expect(api.getApiWeatherMetarTaf).toHaveBeenCalledTimes(1);
    expect(fetcher.fetching()).toBe(false);
  });

  it('drops a report that arrives after the pilot switched to another location, so it never lands in the wrong flight', async () => {
    const pending = new Subject<MetarTafDto>();
    const { fetcher, meteo } = setup(pending);

    const inFlight = fetcher.fetch(meteo);
    fetcher.station.set({ chosen: true, icaoCode: 'EBBR' });
    TestBed.tick();
    pending.next({ station: 'EBAW', metar: METAR, taf: TAF });
    pending.complete();
    await inFlight;

    expect(meteo.controls.metar.value).toBe('');
    expect(fetcher.outcome()).toBeNull();
    expect(fetcher.fetching()).toBe(false);
  });

  it('clears the last message when another location is picked, but not when the same one is re-reported', async () => {
    const { fetcher, meteo } = setup();
    await fetcher.fetch(meteo);

    fetcher.station.set({ chosen: true, icaoCode: 'EBAW' }); // same station, new object: the form recomputes on every keystroke
    TestBed.tick();
    expect(fetcher.outcome()?.kind).toBe('ok');

    fetcher.station.set({ chosen: true, icaoCode: 'EBBR' });
    TestBed.tick();
    expect(fetcher.outcome()).toBeNull();
  });
});
