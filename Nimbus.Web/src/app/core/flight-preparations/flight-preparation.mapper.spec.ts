import type { FlightPreparationDto } from '../api-client';
import { createFlightPreparationForm } from './flight-preparation-form';
import { flightTitle, fromDto, toRequestDto } from './flight-preparation.mapper';
import { ChecklistAnswer } from './flight-preparation.model';
import { NonNullableFormBuilder } from '@angular/forms';
import { TestBed } from '@angular/core/testing';

describe('flight preparation mapper', () => {
  function sampleValue() {
    const form = createFlightPreparationForm(TestBed.inject(NonNullableFormBuilder));
    const value = form.getRawValue();
    value.general = { ...value.general, flightDate: '2026-08-08', flightTime: '18:45', balloonId: 'b', pilotId: 'p', launchLocationId: 'l', sunriseTime: '', sunsetTime: '21:03' };
    value.meteo = { ...value.meteo, windSpeedKt: 6.5, windLevels: [{ altitudeM: 100, directionDeg: 90, speedKt: 5, temperatureC: 12 }] };
    value.checklists = { ...value.checklists, firstAidAndExtinguisher: ChecklistAnswer.Yes, flightPlanFiled: ChecklistAnswer.NotApplicable };
    value.loadCalculation = {
      ...value.loadCalculation,
      envelopeWeightKg: 200,
      basketWeightKg: 90,
      burnerWeightKg: 20,
      cylindersWeightKg: 40,
      passengers: [{ name: 'Alice', weightKg: 62, needsAssistance: false, isChild: false, needsTransport: true }],
    };
    return value;
  }

  it('turns blank optional times into null and checklist names into numbers', () => {
    const request = toRequestDto(sampleValue(), 'Title');
    expect(request.sunriseTime).toBeNull();
    expect(request.sunsetTime).toBe('21:03');
    expect(request.firstAidAndExtinguisher).toBe(1);
    expect(request.passengerListFilled).toBe(0);
    expect(request.flightPlanFiled).toBe(2);
    expect(request.title).toBe('Title');
  });

  it('leaves out the landing-site pick, which has no column', () => {
    const request = toRequestDto(sampleValue(), 'T') as Record<string, unknown>;
    expect(request['landingLocationId']).toBeUndefined();
  });

  it('sends the balloon-weight snapshot with the flight', () => {
    const request = toRequestDto(sampleValue(), 'T');
    expect([request.basketWeightKg, request.burnerWeightKg, request.cylindersWeightKg]).toEqual([90, 20, 40]);
  });

  it('round-trips the form value through the API shape', () => {
    const original = sampleValue();
    const dto: FlightPreparationDto = {
      id: 'x',
      status: 'Planned',
      updatedDate: null,
      details: { ...toRequestDto(original, 'T'), flightTime: '18:45:00', sunsetTime: '21:03:00' },
    };
    const back = fromDto(dto);
    expect(back.general).toEqual(original.general);
    expect(back.meteo).toEqual(original.meteo);
    expect(back.checklists).toEqual(original.checklists);
    expect(back.loadCalculation.passengers).toEqual(original.loadCalculation.passengers);
    expect(back.loadCalculation.envelopeWeightKg).toBe(200);
  });

  it('restores the stored weight snapshot, and zero where a flight predates it', () => {
    const dto: FlightPreparationDto = { id: 'x', status: 'Planned', updatedDate: null, details: toRequestDto(sampleValue(), 'T') };
    expect(fromDto(dto).loadCalculation).toMatchObject({ basketWeightKg: 90, burnerWeightKg: 20, cylindersWeightKg: 40 });
    const legacy = { ...dto, details: { ...dto.details, basketWeightKg: null, burnerWeightKg: undefined, cylindersWeightKg: null } };
    expect(fromDto(legacy).loadCalculation).toMatchObject({ basketWeightKg: 0, burnerWeightKg: 0, cylindersWeightKg: 0 });
  });

  it('builds a list title from the location and date', () => {
    expect(flightTitle('Weelde', '2026-08-08')).toBe('Weelde · 2026-08-08');
    expect(flightTitle(null, '2026-08-08')).toBe('Flight 2026-08-08');
  });
});
