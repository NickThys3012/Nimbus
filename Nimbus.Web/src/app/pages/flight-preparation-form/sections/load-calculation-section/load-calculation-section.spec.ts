import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder } from '@angular/forms';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';

import { FlightAssessmentService, LiftCalculationService } from '../../../../core/api-client';

import {
  createFlightPreparationForm,
  createPassengerGroup,
  hydrateFlightPreparationForm,
} from '../../../../core/flight-preparations/flight-preparation-form';
import { PilotsStore } from '../../../../core/pilots/pilots.store';
import { LoadCalculationSection } from './load-calculation-section';

describe('LoadCalculationSection', () => {
  let fixture: ComponentFixture<LoadCalculationSection>;
  let fb: NonNullableFormBuilder;
  let liftApi: { getApiLiftCalculationTotalLift: ReturnType<typeof vi.fn> };
  let assessApi: { postApiFlightAssessment: ReturnType<typeof vi.fn> };
  let form: ReturnType<typeof createFlightPreparationForm>;

  beforeEach(() => {
    assessApi = {
      postApiFlightAssessment: vi.fn(() =>
        of({ totalWeightKg: 180, passengersWeightKg: 0, marginKg: 120, liftSufficient: true, goNoGoVerdict: 'yellow' }),
      ),
    };
    liftApi = { getApiLiftCalculationTotalLift: vi.fn(() => of({ totalLiftKg: 817.8 })) };
    TestBed.configureTestingModule({
      imports: [LoadCalculationSection],
      providers: [
        { provide: LiftCalculationService, useValue: liftApi },
        { provide: FlightAssessmentService, useValue: assessApi },
        { provide: PilotsStore, useValue: { list: signal([{ id: 'p1', name: 'N. Thys', weightKg: 80 }]) } }],
    });
    fb = TestBed.inject(NonNullableFormBuilder);
    form = createFlightPreparationForm(fb);
    fixture = TestBed.createComponent(LoadCalculationSection);
    fixture.componentRef.setInput('group', form.controls.loadCalculation);
    fixture.componentRef.setInput('pilotId', 'p1');
    fixture.detectChanges();
  });

  /** Lets the debounced assessment request fire and its response render. */
  const settleAssessment = async () => {
    await new Promise((r) => setTimeout(r, 200));
    fixture.detectChanges();
  };

  const el = (id: string) => fixture.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLElement;
  const type = (id: string, value: string) => {
    const input = el(id) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };
  const addPassenger = (name: string, weight: number) => {
    el('add-passenger').click();
    fixture.detectChanges();
    const i = form.controls.loadCalculation.controls.passengers.length - 1;
    type(`passenger-name-${i}`, name);
    type(`passenger-weight-${i}`, String(weight));
  };
  const names = () => form.controls.loadCalculation.controls.passengers.getRawValue().map((p) => p.name);

  it('shows the server assessment and sends only raw values (no local calculation)', async () => {
    type('envelope-weight', '100');
    addPassenger('A', 70);
    await settleAssessment();
    const request = assessApi.postApiFlightAssessment.mock.calls.at(-1)![0].requestBody;
    expect(request).toEqual(
      expect.objectContaining({ envelopeWeightKg: 100, pilotWeightKg: 80, passengerWeightsKg: [70], windSpeedKt: null }),
    );
    expect(el('total-weight').textContent).toContain('180 kg'); // the mocked server answer, not a client sum
    expect(el('go-no-go-verdict').textContent).toContain('yellow');
  });

  it('sends the meteo values to the assessment', async () => {
    fixture.componentRef.setInput('weather', { windSpeedKt: 12, visibilityKm: 8, capeJPerKg: 100 });
    fixture.detectChanges();
    await settleAssessment();
    expect(assessApi.postApiFlightAssessment.mock.calls.at(-1)![0].requestBody).toEqual(
      expect.objectContaining({ windSpeedKt: 12, visibilityKm: 8, capeJPerKg: 100 }),
    );
  });

  it('renders the lift verdict from the server assessment', async () => {
    const answer = (liftSufficient: boolean | null, marginKg: number) =>
      assessApi.postApiFlightAssessment.mockReturnValue(
        of({ totalWeightKg: 180, passengersWeightKg: 0, marginKg, liftSufficient, goNoGoVerdict: 'green' }),
      );
    answer(null, 0);
    type('envelope-weight', '100');
    await settleAssessment();
    expect(el('load-verdict').dataset['state']).toBe('unknown');
    answer(true, 120);
    type('total-lift', '300');
    await settleAssessment();
    expect(el('load-verdict').dataset['state']).toBe('ok');
    answer(false, -30);
    type('total-lift', '150');
    await settleAssessment();
    expect(el('load-verdict').dataset['state']).toBe('over');
  });

  it('falls back to a neutral state when the assessment fails', async () => {
    assessApi.postApiFlightAssessment.mockReturnValue(throwError(() => new Error('boom')));
    type('envelope-weight', '100');
    await settleAssessment();
    expect(el('load-verdict').dataset['state']).toBe('unknown');
    expect(el('go-no-go-verdict').textContent).toContain('unknown');
  });

  it('does not store derived values in the form', () => {
    const keys = Object.keys(form.controls.loadCalculation.getRawValue());
    expect(keys).not.toContain('totalWeightKg');
    expect(keys).not.toContain('marginKg');
  });

  it('adds, reorders and removes passengers, keeping the order', () => {
    addPassenger('A', 70);
    addPassenger('B', 60);
    addPassenger('C', 50);
    el('move-up-passenger-2').click();
    fixture.detectChanges();
    expect(names()).toEqual(['A', 'C', 'B']);
    el('move-down-passenger-0').click();
    fixture.detectChanges();
    expect(names()).toEqual(['C', 'A', 'B']);
    el('remove-passenger-1').click();
    fixture.detectChanges();
    expect(names()).toEqual(['C', 'B']);
    expect((el('move-up-passenger-0') as HTMLButtonElement).disabled).toBe(true);
  });

  it('keeps passenger order and flags across a save and reload', () => {
    const lc = form.controls.loadCalculation;
    lc.controls.passengers.push(createPassengerGroup(fb));
    lc.controls.passengers.push(createPassengerGroup(fb));
    lc.controls.passengers.at(0).patchValue({ name: 'First', weightKg: 70, needsTransport: true });
    lc.controls.passengers.at(1).patchValue({ name: 'Second', weightKg: 60, isChild: true, needsAssistance: true });
    const saved = JSON.parse(JSON.stringify(form.getRawValue()));

    const reloaded = createFlightPreparationForm(fb);
    hydrateFlightPreparationForm(reloaded, fb, saved);
    const passengers = reloaded.controls.loadCalculation.controls.passengers.getRawValue();
    expect(passengers.map((p) => p.name)).toEqual(['First', 'Second']);
    expect(passengers[0].needsTransport).toBe(true);
    expect(passengers[1]).toEqual(expect.objectContaining({ isChild: true, needsAssistance: true }));
  });

  describe('calculating total lift', () => {
    const settle = async () => {
      await fixture.whenStable();
      fixture.detectChanges();
    };

    it('is disabled until a balloon provides the envelope volume', () => {
      expect((el('calculate-lift') as HTMLButtonElement).disabled).toBe(true);
      expect(el('calculate-lift-hint')).not.toBeNull();
    });

    it('stays disabled when the balloon provides no temperature', () => {
      fixture.componentRef.setInput('liftContext', { envelopeVolumeM3: 3000, internalTemperatureC: null, siteElevationM: 0, ambientTemperatureC: 15 });
      fixture.detectChanges();
      expect((el('calculate-lift') as HTMLButtonElement).disabled).toBe(true);
    });

    it('sends the manual formula inputs (altitude converted to metres) and prefills an editable total lift', async () => {
      fixture.componentRef.setInput('liftContext', { envelopeVolumeM3: 3000, internalTemperatureC: 95, siteElevationM: 50, ambientTemperatureC: 15 });
      fixture.detectChanges();
      type('max-altitude', '3280.84');
      el('calculate-lift').click();
      await settle();
      expect(liftApi.getApiLiftCalculationTotalLift).toHaveBeenCalledWith({
        maxAltitudeMeters: 1000,
        siteElevationMeters: 50,
        ambientTemperatureC: 15,
        internalTemperatureC: 95,
        envelopeVolumeM3: 3000,
      });
      expect(form.controls.loadCalculation.controls.totalLiftKg.value).toBe(817.8);
      type('total-lift', '800');
      expect(form.controls.loadCalculation.controls.totalLiftKg.value).toBe(800);
    });

    it('shows an error and keeps the typed value when the calculation fails', async () => {
      liftApi.getApiLiftCalculationTotalLift.mockReturnValue(throwError(() => new Error('boom')));
      fixture.componentRef.setInput('liftContext', { envelopeVolumeM3: 3000, internalTemperatureC: 95, siteElevationM: 0, ambientTemperatureC: 15 });
      fixture.detectChanges();
      type('total-lift', '500');
      el('calculate-lift').click();
      await settle();
      expect(el('calculate-lift-error')).not.toBeNull();
      expect(form.controls.loadCalculation.controls.totalLiftKg.value).toBe(500);
    });
  });
});
