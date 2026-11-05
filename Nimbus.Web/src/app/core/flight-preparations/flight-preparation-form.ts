import { AbstractControl, FormArray, NonNullableFormBuilder, Validators } from '@angular/forms';

import { type SectionCompletionStatus } from './flight-preparation.model';

/**
 * Builds the single reactive form that drives the whole nine-section dossier (issue #32): one
 * `FormGroup` per section, nested under a parent group so the dossier's overall validity/dirty
 * state is always known from one place, while each section component only ever sees its own
 * slice (`form.controls.general`, `form.controls.meteo`, ...).
 *
 * Field names mirror the flat columns documented in `docs/flight-preparation.md` 1:1, so mapping
 * `getRawValue()` onto the real `FlightPreparation` request DTO (once it exists) is a reshape, not
 * a redesign.
 */
export function createFlightPreparationForm(fb: NonNullableFormBuilder) {
  return fb.group({
    general: fb.group({
      flightDate: ['', Validators.required],
      flightTime: ['', Validators.required],
      balloonId: ['', Validators.required],
      pilotId: ['', Validators.required],
      launchLocationId: ['', Validators.required],
      sunriseTime: [''],
      sunsetTime: [''],
      fieldOwnerNotified: [false],
    }),
    meteo: fb.group({
      windSpeedKt: [0, [Validators.required, Validators.min(0)]],
      windDirectionDeg: [0, [Validators.required, Validators.min(0), Validators.max(360)]],
      visibilityKm: [0, [Validators.min(0)]],
      capeJPerKg: [0],
      temperatureC: [0],
      dewPointC: [0],
      qnhHpa: [0],
      metar: [''],
      taf: [''],
      windLevels: fb.array([createWindLevelGroup(fb)]),
    }),
    airspace: fb.group({
      notamsChecked: [false, Validators.requiredTrue],
      airspaceStructureNotes: [''],
      airspaceRestrictions: [''],
      obstacles: [''],
    }),
    checklists: fb.group({
      items: fb.array(DEFAULT_CHECKLIST_ITEMS.map((label) => createChecklistItemGroup(fb, label))),
    }),
    loadCalculation: fb.group({
      envelopeWeightKg: [0, [Validators.required, Validators.min(0)]],
      maxAltitudeFt: [0, [Validators.required, Validators.min(0)]],
      loadRemarks: [''],
      passengers: fb.array<ReturnType<typeof createPassengerGroup>>([]),
    }),
    route: fb.group({
      routeDescription: [''],
      climbRateMetersPerSecond: [0, [Validators.min(0)]],
      descentRateMetersPerSecond: [0, [Validators.min(0)]],
      cruiseAltitudesFtCsv: [''],
    }),
    bulletin: fb.group({
      bulletinText: [''],
    }),
    ofp: fb.group({
      ofpOperatorName: ['', Validators.required],
      ofpPicWeightKg: [0, [Validators.min(0)]],
      ofpFuelAvailableMinutes: [0, [Validators.min(0)]],
      ofpFuelRequiredMinutes: [0, [Validators.min(0)]],
      ofpLandingLocation: [''],
    }),
    paxBriefing: fb.group({
      paxBriefingText: [''],
      paxBriefingVersion: [''],
      paxBriefingIsCustom: [false],
    }),
  });
}

export function createWindLevelGroup(fb: NonNullableFormBuilder) {
  return fb.group({
    altitudeM: [0, [Validators.required, Validators.min(0)]],
    directionDeg: [0, [Validators.required, Validators.min(0), Validators.max(360)]],
    speedKt: [0, [Validators.required, Validators.min(0)]],
    temperatureC: [0],
  });
}

export function createPassengerGroup(fb: NonNullableFormBuilder) {
  return fb.group({
    name: ['', Validators.required],
    weightKg: [0, [Validators.required, Validators.min(1)]],
    needsAssistance: [false],
    isChild: [false],
  });
}

export function createChecklistItemGroup(fb: NonNullableFormBuilder, label: string) {
  return fb.group({
    label: [label, Validators.required],
    checked: [false],
  });
}

/** Starter checklist, grouped like the mockup's "safety & comms" / "technical check" sections. */
export const DEFAULT_CHECKLIST_ITEMS: readonly string[] = [
  'First-aid kit & fire extinguisher',
  'Passenger list completed',
  'Flight plan filed',
  'Burner tested',
  'Gas cylinders checked',
  'Envelope visually inspected',
  'Mooring lines checked',
  'Instruments working',
];

export type FlightPreparationForm = ReturnType<typeof createFlightPreparationForm>;
export type FlightPreparationFormValue = ReturnType<FlightPreparationForm['getRawValue']>;
export type GeneralFormGroup = FlightPreparationForm['controls']['general'];
export type MeteoFormGroup = FlightPreparationForm['controls']['meteo'];
export type AirspaceFormGroup = FlightPreparationForm['controls']['airspace'];
export type ChecklistsFormGroup = FlightPreparationForm['controls']['checklists'];
export type LoadCalculationFormGroup = FlightPreparationForm['controls']['loadCalculation'];
export type RouteFormGroup = FlightPreparationForm['controls']['route'];
export type BulletinFormGroup = FlightPreparationForm['controls']['bulletin'];
export type OfpFormGroup = FlightPreparationForm['controls']['ofp'];
export type PaxBriefingFormGroup = FlightPreparationForm['controls']['paxBriefing'];

/**
 * Rebuilds a freshly-constructed form's dynamic `FormArray`s (wind levels, passengers, checklist
 * items) to match a loaded draft's shape, then patches in every value. Needed because
 * `FormGroup.reset()` only resets *existing* controls — it can't grow/shrink a `FormArray` to a
 * different saved flight's number of rows, which is why switching between two drafts rebuilds the
 * whole form (see `FlightPreparationDraftStore` / the form page) rather than reusing one instance.
 */
export function hydrateFlightPreparationForm(
  form: FlightPreparationForm,
  fb: NonNullableFormBuilder,
  value: FlightPreparationFormValue,
): void {
  resizeArray(form.controls.meteo.controls.windLevels, value.meteo.windLevels.length, () => createWindLevelGroup(fb));
  resizeArray(form.controls.loadCalculation.controls.passengers, value.loadCalculation.passengers.length, () =>
    createPassengerGroup(fb),
  );
  resizeArray(form.controls.checklists.controls.items, value.checklists.items.length, () =>
    createChecklistItemGroup(fb, ''),
  );
  form.patchValue(value);
}

function resizeArray<T extends AbstractControl>(array: FormArray<T>, targetLength: number, create: () => T): void {
  while (array.length < targetLength) array.push(create());
  while (array.length > targetLength) array.removeAt(array.length - 1);
}

/**
 * A section's completion dot: `'complete'` once it's valid and has been touched/edited,
 * `'invalid'` once it's been touched/submitted but fails validation, `'pending'` while untouched.
 * Optional-only sections (no required fields) read as `'complete'` as soon as they're touched —
 * there's nothing further for the pilot to complete.
 */
export function sectionCompletionStatus(group: AbstractControl): SectionCompletionStatus {
  const touched = group.touched || group.dirty;
  if (group.invalid) return touched ? 'invalid' : 'pending';
  return touched ? 'complete' : 'pending';
}

