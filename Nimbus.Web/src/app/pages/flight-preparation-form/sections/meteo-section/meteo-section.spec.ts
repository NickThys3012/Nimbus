import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder } from '@angular/forms';
import { of, Subject, throwError } from 'rxjs';
import { type Mock, vi } from 'vitest';

import { MeteoSection, NO_ICAO_MESSAGE, NO_LOCATION_MESSAGE } from './meteo-section';
import { type MetarTafDto, WeatherService } from '../../../../core/api-client';
import { createFlightPreparationForm } from '../../../../core/flight-preparations/flight-preparation-form';

@Component({
  imports: [MeteoSection],
  template: `<Nimbus-meteo-section [group]="form.controls.meteo" [icaoCode]="icao()" [locationChosen]="chosen()" />`,
})
class Host {
  form = createFlightPreparationForm(TestBed.inject(NonNullableFormBuilder));
  icao = signal<string | null>('EBAW');
  chosen = signal(true);
}

const TAF = 'TAF EBAW 100800Z 1009/1018 09006KT 0500 FG BKN001\n  BECMG 1009/1011 6000 NSW SCT010';

describe('MeteoSection METAR/TAF fetch (issue #44)', () => {
  let api: { getApiWeatherMetarTaf: Mock };

  async function setup(response: MetarTafDto | Error | Subject<MetarTafDto> = { station: 'EBAW', metar: 'EBAW 100850Z 08003KT 0400 FG Q1013', taf: TAF }) {
    api = {
      getApiWeatherMetarTaf: vi.fn().mockReturnValue(response instanceof Error ? throwError(() => response) : response instanceof Subject ? response : of(response)),
    };
    await TestBed.configureTestingModule({ providers: [{ provide: WeatherService, useValue: api }] }).compileComponents();
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const q = (id: string) => el.querySelector(`[data-testid="${id}"]`) as HTMLElement | null;
    const metar = () => q('metar') as HTMLTextAreaElement;
    const taf = () => q('taf') as HTMLTextAreaElement;
    const button = () => q('fetch-metar-taf')!.querySelector('button') as HTMLButtonElement;
    const flush = async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();
    };
    const fetch = async () => {
      button().click();
      await flush();
    };
    const type = async (control: 'metar' | 'taf', value: string) => {
      fixture.componentInstance.form.controls.meteo.controls[control].setValue(value);
      await flush();
    };
    return { fixture, host: fixture.componentInstance, q, metar, taf, button, fetch, flush, type };
  }

  it('fetches both reports for the launch location ICAO and fills the fields', async () => {
    const { fetch, metar, q } = await setup();

    await fetch();

    expect(api.getApiWeatherMetarTaf).toHaveBeenCalledWith({ icao: 'EBAW' });
    expect(metar().value).toBe('EBAW 100850Z 08003KT 0400 FG Q1013');
    expect(q('fetch-ok')?.textContent).toContain('Fetched the METAR and TAF for EBAW');
  });

  it('keeps the TAF as its full multi-line text', async () => {
    const { fetch, taf } = await setup();

    await fetch();

    expect(taf().value).toBe(TAF);
    expect(taf().value.split('\n')).toHaveLength(2);
    expect(taf().value.split('\n')[1]).toBe('  BECMG 1009/1011 6000 NSW SCT010');
  });

  it('counts as an edit of the section, so the draft is kept and the section is marked edited', async () => {
    const { fetch, host } = await setup();
    expect(host.form.controls.meteo.dirty).toBe(false);

    await fetch();

    expect(host.form.controls.meteo.controls.metar.dirty).toBe(true);
    expect(host.form.controls.meteo.controls.taf.dirty).toBe(true);
    expect(host.form.controls.meteo.dirty).toBe(true);
  });

  it('replaces what was there with the fresh report, because the pilot asked for it', async () => {
    const { fetch, type, metar } = await setup();
    await type('metar', 'old text');

    await fetch();

    expect(metar().value).toBe('EBAW 100850Z 08003KT 0400 FG Q1013');
  });

  describe('without an ICAO code', () => {
    it('is prevented, with the reason shown, when the launch location has none', async () => {
      const { host, flush, button, q, fetch } = await setup();
      host.icao.set(null);
      await flush();

      expect(button().disabled).toBe(true);
      expect(q('fetch-blocked')?.textContent).toBe(NO_ICAO_MESSAGE);
      expect(NO_ICAO_MESSAGE).toContain('no ICAO code');

      await fetch();
      expect(api.getApiWeatherMetarTaf).not.toHaveBeenCalled();
    });

    it('is prevented, with a different reason, when no launch location is picked yet', async () => {
      const { host, flush, button, q } = await setup();
      host.icao.set(null);
      host.chosen.set(false);
      await flush();

      expect(button().disabled).toBe(true);
      expect(q('fetch-blocked')?.textContent).toBe(NO_LOCATION_MESSAGE);
    });

    it('is possible again as soon as a location with a code is picked', async () => {
      const { host, flush, button, q } = await setup();
      host.icao.set(null);
      await flush();
      host.icao.set('EBBR');
      await flush();

      expect(button().disabled).toBe(false);
      expect(q('fetch-blocked')).toBeNull();
    });
  });

  describe('when the provider fails', () => {
    it('tells the pilot, leaves the existing values exactly as they were, and does not throw', async () => {
      const { fetch, type, metar, taf, q, host } = await setup(new Error('HTTP 502'));
      await type('metar', 'my own METAR');
      await type('taf', 'my own TAF');
      host.form.controls.meteo.markAsPristine();

      await expect(fetch()).resolves.toBeUndefined();

      expect(metar().value).toBe('my own METAR');
      expect(taf().value).toBe('my own TAF');
      expect(q('fetch-failed')?.textContent).toContain('Your entries are unchanged');
      expect(host.form.controls.meteo.dirty).toBe(false);
    });

    it('can be tried again, and a retry that works fills the fields', async () => {
      const { fetch, q, metar } = await setup(new Error('HTTP 502'));
      await fetch();
      expect(q('fetch-failed')).not.toBeNull();

      api.getApiWeatherMetarTaf.mockReturnValue(of({ station: 'EBAW', metar: 'EBAW 100850Z 08003KT 0400 FG Q1013', taf: TAF }));
      await fetch();

      expect(q('fetch-failed')).toBeNull();
      expect(metar().value).toContain('EBAW 100850Z');
    });
  });

  describe('when the station does not publish everything', () => {
    it('fills the METAR and leaves the TAF alone when there is no TAF', async () => {
      const { fetch, type, metar, taf, q } = await setup({ station: 'EBOS', metar: 'EBOS 100850Z 09004KT 9999 Q1013', taf: null });
      await type('taf', 'my own TAF');

      await fetch();

      expect(metar().value).toBe('EBOS 100850Z 09004KT 9999 Q1013');
      expect(taf().value).toBe('my own TAF');
      expect(q('fetch-partial')?.textContent).toContain('No TAF is published');
    });

    it('fills the TAF and leaves the METAR alone when there is no METAR', async () => {
      const { fetch, type, metar, taf, q } = await setup({ station: 'EBOS', metar: null, taf: TAF });
      await type('metar', 'my own METAR');

      await fetch();

      expect(metar().value).toBe('my own METAR');
      expect(taf().value).toBe(TAF);
      expect(q('fetch-partial')?.textContent).toContain('No METAR is published');
    });

    it('changes nothing and says so when nothing is published at all', async () => {
      const { fetch, type, metar, taf, q, host } = await setup({ station: 'ZZZZ', metar: null, taf: null });
      await type('metar', 'my own METAR');
      await type('taf', 'my own TAF');
      host.form.controls.meteo.markAsPristine();

      await fetch();

      expect(metar().value).toBe('my own METAR');
      expect(taf().value).toBe('my own TAF');
      expect(q('fetch-none')?.textContent).toContain('No METAR or TAF is published for ZZZZ');
      expect(host.form.controls.meteo.dirty).toBe(false);
    });
  });

  it('sends one request however often the button is clicked while it is waiting', async () => {
    const pending = new Subject<MetarTafDto>();
    const { fetch, q } = await setup(pending);

    await fetch();
    await fetch();

    expect(api.getApiWeatherMetarTaf).toHaveBeenCalledTimes(1);
    expect(q('fetch-metar-taf')?.textContent).toContain('Fetching…');
    expect((q('fetch-metar-taf')!.querySelector('button') as HTMLButtonElement).disabled).toBe(true);
  });

  it("drops a report that arrives after the pilot switched to another location, so it never lands in the wrong flight", async () => {
    const pending = new Subject<MetarTafDto>();
    const { fetch, host, flush, metar, q } = await setup(pending);

    await fetch();
    host.icao.set('EBBR');
    await flush();
    pending.next({ station: 'EBAW', metar: 'EBAW 100850Z 08003KT 0400 FG Q1013', taf: TAF });
    pending.complete();
    await flush();

    expect(metar().value).toBe('');
    expect(q('fetch-ok')).toBeNull();
  });

  it('clears the last message when another location is picked', async () => {
    const { fetch, host, flush, q } = await setup();
    await fetch();
    expect(q('fetch-ok')).not.toBeNull();

    host.icao.set('EBBR');
    await flush();

    expect(q('fetch-ok')).toBeNull();
  });
});
