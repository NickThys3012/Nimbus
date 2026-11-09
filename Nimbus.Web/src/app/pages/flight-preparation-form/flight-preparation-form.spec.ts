import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';

import FlightPreparationFormPage from './flight-preparation-form';
import { AuthStore } from '../../core/auth/auth.store';
import { BalloonsService, FlightAssessmentService, LaunchLocationsService, LiftCalculationService, PilotsService } from '../../core/api-client';
import { LOCAL_STORAGE } from '../../services/theme-service';

/**
 * Covers the issue #32 mockup-driven UI on top of the nine-section form (segmented progress bar,
 * dirty/draft badges, save-error banner, the async "Leave without saving?" dialog). The reactive
 * form's own validity/section-building behaviour is covered by
 * `core/flight-preparations/flight-preparation-form.spec.ts`-style unit tests; this file is about
 * what the page renders and wires up around it.
 */
describe('FlightPreparationFormPage', () => {
  let email: string | null;
  let mockStore: Record<string, string>;
  let mockStorage: Storage;
  let navigate: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    mockStore = {};
    mockStorage = {
      getItem: (key: string) => mockStore[key] ?? null,
      setItem: (key: string, val: string) => { mockStore[key] = val; },
      removeItem: (key: string) => { delete mockStore[key]; },
      clear: () => { Object.keys(mockStore).forEach((k) => delete mockStore[k]); },
      key: (index: number) => Object.keys(mockStore)[index] ?? null,
      get length() { return Object.keys(mockStore).length; },
    } as Storage;
    email = 'pilot.a@example.com';
  });

  const balloon = {
    id: 'b1', registration: 'OO-ABC', type: 'X', envelopeVolumeM3: 3000, maxEnvelopeTemperatureC: 120,
    envelopeWeightKg: 118, basketWeightKg: 90, burnerWeightKg: 25, cylindersWeightKg: 60, emptyWeightKg: 293,
  };
  const location = { id: 'l1', name: 'Weelde', icaoCode: null, latitude: 51.4, longitude: 4.9, elevationM: null, airspaceNotes: null };

  async function createComponent(routeId: string | null = null, balloons: unknown[] = [], locations: unknown[] = []) {
    navigate = vi.fn();

    await TestBed.configureTestingModule({
      imports: [FlightPreparationFormPage],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { email: () => email } },
        { provide: LOCAL_STORAGE, useValue: mockStorage },
        { provide: BalloonsService, useValue: { getApiBalloons: () => of(balloons) } },
        { provide: PilotsService, useValue: { getApiPilots: () => of([]) } },
        { provide: LiftCalculationService, useValue: { getApiLiftCalculationTotalLift: () => of({ totalLiftKg: 0 }) } },
        {
          provide: FlightAssessmentService,
          useValue: {
            postApiFlightAssessment: () =>
              of({ totalWeightKg: 0, passengersWeightKg: 0, marginKg: 0, liftSufficient: null, goNoGoVerdict: 'unknown' }),
          },
        },
        { provide: LaunchLocationsService, useValue: { getApiLaunchLocations: () => of(locations) } },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: convertToParamMap(routeId ? { id: routeId } : {}) },
            paramMap: of(convertToParamMap(routeId ? { id: routeId } : {})),
          },
        },
        { provide: Router, useValue: { navigate } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(FlightPreparationFormPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const q = <T extends HTMLElement>(id: string) => el.querySelector(`[data-testid="${id}"]`) as T | null;
    const click = async (id: string) => {
      const target = q(id);
      (target?.querySelector('button') ?? target)?.dispatchEvent(new Event('click', { bubbles: true }));
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();
    };
    const expandSection = async (id: string) => {
      const header = el.querySelector(`[data-section="${id}"] .card-head`) as HTMLButtonElement | null;
      header?.click();
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();
    };
    return { fixture, el, q, click, expandSection };
  }

  function seedSavedFlight(id: string, overrides: Record<string, unknown> = {}) {
    const baseline = {
      general: {
        flightDate: '2026-08-08', flightTime: '18:45', balloonId: 'b1', pilotId: 'p1',
        launchLocationId: 'l1', sunriseTime: '', sunsetTime: '', fieldOwnerNotified: false,
      },
      meteo: {
        windSpeedKt: 8, windDirectionDeg: 240, visibilityKm: 10, capeJPerKg: 0, temperatureC: 0,
        dewPointC: 0, qnhHpa: 1018, metar: '', taf: '', windLevels: [],
      },
      airspace: { notamsChecked: true, airspaceStructureNotes: '', airspaceRestrictions: '', obstacles: '' },
      checklists: {
        firstAidAndExtinguisher: 'No', passengerListFilled: 'No', flightPlanFiled: 'No', burnerTested: false,
        gasCylindersChecked: false, balloonVisualInspectionDone: false, mooringsChecked: false, instrumentsChecked: false,
      },
      loadCalculation: { envelopeWeightKg: 118, maxAltitudeFt: 4500, loadRemarks: '', passengers: [] },
      route: { routeDescription: '', climbRateMetersPerSecond: 0, descentRateMetersPerSecond: 0, cruiseAltitudesFtCsv: '' },
      bulletin: { bulletinText: '' },
      ofp: { ofpOperatorName: 'Nimbus Balloons', ofpPicWeightKg: 82, ofpFuelAvailableMinutes: 0, ofpFuelRequiredMinutes: 0, ofpLandingLocation: '' },
      paxBriefing: { paxBriefingText: '', paxBriefingVersion: '', paxBriefingIsCustom: false },
      ...overrides,
    };
    mockStore[`nimbus.flight-prep.pilot.a@example.com.${id}`] = JSON.stringify({
      value: baseline,
      updatedAt: '2026-08-08T17:42:00.000Z',
    });
    return baseline;
  }

  it('renders one progress segment per section and a completion counter', async () => {
    const { q } = await createComponent();

    const segments = q('progress-segments')!.querySelectorAll('.segment');
    expect(segments.length).toBe(9);
    expect(q('progress-count')?.textContent).toContain('/9 complete');
  });

  it('defaults a new flight to the next sensible date and time instead of empty', async () => {
    const { q } = await createComponent();

    expect(q<HTMLInputElement>('flight-date')?.value).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    expect(q<HTMLInputElement>('flight-time')?.value).toMatch(/^(07|19):00$/);
  });

  it('snapshots the picked balloon weights onto a new flight', async () => {
    const { fixture, q } = await createComponent(null, [balloon]);
    const select = q<HTMLSelectElement>('balloon')!;
    select.value = 'b1';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    const load = fixture.componentInstance['form']().controls.loadCalculation.getRawValue();
    expect(load.envelopeWeightKg).toBe(118);
    expect(load.basketWeightKg).toBe(90);
    expect(load.burnerWeightKg).toBe(25);
    expect(load.cylindersWeightKg).toBe(60);
  });

  it('does not overwrite the snapshot of an already saved flight when its balloon changes', async () => {
    seedSavedFlight('flight-1');
    const { fixture, q } = await createComponent('flight-1', [balloon]);
    const select = q<HTMLSelectElement>('balloon')!;
    select.value = 'b1';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(fixture.componentInstance['form']().controls.loadCalculation.getRawValue().basketWeightKg).toBe(0);
  });

  it('exposes the selected launch location coordinates and offers a landing location', async () => {
    const { fixture, q } = await createComponent(null, [], [location]);
    expect(q('launch-coordinates')).toBeNull();
    expect(q('landing-location')).not.toBeNull();

    const select = q<HTMLSelectElement>('launch-location')!;
    select.value = 'l1';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(fixture.componentInstance.launchCoordinates()).toEqual({ latitude: 51.4, longitude: 4.9 });
    expect(q('launch-coordinates')?.textContent).toContain('51.4');
  });

  it('shows a save-error banner naming the invalid sections and expands the first one when saving an empty form', async () => {
    const { q, click } = await createComponent();

    await click('save-flight-prep');

    const banner = q('save-error-banner');
    expect(banner?.textContent).toContain('Not saved.');
    expect(banner?.textContent).toContain('General');
    // The first invalid section (General) must be auto-expanded.
    expect(q('flight-date')).not.toBeNull();
  });

  it('marks a section dirty ("EDITED") once an existing flight is edited, and reflects it in the sticky-bar summary', async () => {
    seedSavedFlight('flight-1');
    const { fixture, el, q, expandSection } = await createComponent('flight-1');

    expect(q('section-badge')).toBeNull();
    expect(q('dirty-summary')).toBeNull();

    await expandSection('Meteo');
    const windSpeed = el.querySelector<HTMLInputElement>('[data-testid="wind-speed"]');
    windSpeed!.value = '12';
    windSpeed!.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(q('dirty-summary')?.textContent).toContain('Meteo');
  });

  it('shows a draft-restored banner with a "FROM DRAFT" badge when a newer autosave differs from the saved baseline, and "Discard draft" clears it', async () => {
    const baseline = seedSavedFlight('flight-1');
    const autosaveValue = { ...baseline, meteo: { ...baseline.meteo, windSpeedKt: 20 } };
    mockStore['nimbus.flight-prep.autosave.pilot.a@example.com.flight-1'] = JSON.stringify({
      value: autosaveValue,
      updatedAt: '2026-08-08T18:02:00.000Z',
    });

    const { q, click } = await createComponent('flight-1');

    expect(q('draft-restored-banner')).not.toBeNull();
    expect(q('section-badge')?.textContent).toContain('FROM DRAFT');

    await click('discard-draft');

    expect(q('draft-restored-banner')).toBeNull();
    expect(q('section-badge')).toBeNull();
  });

  describe('"Leave without saving?" dialog (unsaved-changes guard contract)', () => {
    it('opens with the destination and dirty section names, and "stay" resolves false without navigating', async () => {
      seedSavedFlight('flight-1');
      const { fixture, el, q, click, expandSection } = await createComponent('flight-1');
      const component = fixture.componentInstance;

      await expandSection('Meteo');
      const windSpeed = el.querySelector<HTMLInputElement>('[data-testid="wind-speed"]');
      windSpeed!.value = '12';
      windSpeed!.dispatchEvent(new Event('input'));

      let resolved: boolean | undefined;
      component.confirmDiscard('/logbook').subscribe((leave) => (resolved = leave));
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();

      expect(q('going-to')?.textContent).toContain('/logbook');
      expect(q('leave-copy')?.textContent).toContain('Meteo');

      await click('stay-on-page');

      expect(resolved).toBe(false);
    });

    it('"Discard changes and leave" resolves true without saving', async () => {
      seedSavedFlight('flight-1');
      const { fixture, el, click, expandSection } = await createComponent('flight-1');
      const component = fixture.componentInstance;

      await expandSection('Meteo');
      const windSpeed = el.querySelector<HTMLInputElement>('[data-testid="wind-speed"]');
      windSpeed!.value = '12';
      windSpeed!.dispatchEvent(new Event('input'));

      let resolved: boolean | undefined;
      component.confirmDiscard('/logbook').subscribe((leave) => (resolved = leave));
      fixture.detectChanges();
      await fixture.whenStable();

      await click('leave-without-saving');

      expect(resolved).toBe(true);
      expect(mockStore['nimbus.flight-prep.pilot.a@example.com.flight-1']).toContain('"windSpeedKt":8');
    });

    it('"Save and leave" saves first and only resolves true once the dossier is valid', async () => {
      seedSavedFlight('flight-1');
      const { fixture, click } = await createComponent('flight-1');
      const component = fixture.componentInstance;

      let resolved: boolean | undefined;
      component.confirmDiscard('/logbook').subscribe((leave) => (resolved = leave));
      fixture.detectChanges();
      await fixture.whenStable();

      await click('save-and-leave');

      expect(resolved).toBe(true);
    });
  });
});
