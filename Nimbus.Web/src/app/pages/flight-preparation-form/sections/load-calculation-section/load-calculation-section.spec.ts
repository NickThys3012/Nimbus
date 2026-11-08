import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder } from '@angular/forms';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';

import { LiftCalculationService } from '../../../../core/api-client';

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
  let form: ReturnType<typeof createFlightPreparationForm>;

  beforeEach(() => {
    liftApi = { getApiLiftCalculationTotalLift: vi.fn(() => of({ totalLiftKg: 817.8 })) };
    TestBed.configureTestingModule({
      imports: [LoadCalculationSection],
      providers: [
        { provide: LiftCalculationService, useValue: liftApi },
        { provide: PilotsStore, useValue: { list: signal([{ id: 'p1', name: 'N. Thys', weightKg: 80 }]) } }],
    });
    fb = TestBed.inject(NonNullableFormBuilder);
    form = createFlightPreparationForm(fb);
    fixture = TestBed.createComponent(LoadCalculationSection);
    fixture.componentRef.setInput('group', form.controls.loadCalculation);
    fixture.componentRef.setInput('pilotId', 'p1');
    fixture.detectChanges();
  });

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

  it('recomputes the total live as envelope, pilot and passenger weights change', () => {
    type('envelope-weight', '100');
    expect(el('total-weight').textContent).toContain('180 kg'); // envelope + pilot
    addPassenger('A', 70);
    expect(el('total-weight').textContent).toContain('250 kg');
    type('passenger-weight-0', '90');
    expect(el('total-weight').textContent).toContain('270 kg');
    fixture.componentRef.setInput('pilotId', '');
    fixture.detectChanges();
    expect(el('total-weight').textContent).toContain('190 kg');
  });

  it('indicates whether lift exceeds weight', () => {
    type('envelope-weight', '100');
    expect(el('load-verdict').dataset['state']).toBe('unknown');
    type('total-lift', '300');
    expect(el('load-verdict').dataset['state']).toBe('ok');
    type('total-lift', '150');
    expect(el('load-verdict').dataset['state']).toBe('over');
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

    it('sends the manual formula inputs (altitude converted to metres) and prefills an editable total lift', async () => {
      fixture.componentRef.setInput('liftContext', { envelopeVolumeM3: 3000, siteElevationM: 50, ambientTemperatureC: 15 });
      fixture.detectChanges();
      type('max-altitude', '3280.84');
      type('internal-temperature', '95');
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
      fixture.componentRef.setInput('liftContext', { envelopeVolumeM3: 3000, siteElevationM: 0, ambientTemperatureC: 15 });
      fixture.detectChanges();
      type('total-lift', '500');
      el('calculate-lift').click();
      await settle();
      expect(el('calculate-lift-error')).not.toBeNull();
      expect(form.controls.loadCalculation.controls.totalLiftKg.value).toBe(500);
    });
  });
});
