import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { catchError, debounceTime, firstValueFrom, of, startWith, switchMap } from 'rxjs';

import { FlightAssessmentDto, FlightAssessmentRequestDto, FlightAssessmentService, LiftCalculationService } from '../../../../core/api-client';

import {
  createPassengerGroup,
  type LoadCalculationFormGroup,
  movePassenger,
} from '../../../../core/flight-preparations/flight-preparation-form';
import { buildAssessmentRequest, type WeatherContext } from '../../../../core/flight-preparations/assessment-request';
import { PilotsStore } from '../../../../core/pilots/pilots.store';
import { FieldErrorPipe } from '../field-error.pipe';

/** What section 05 needs from other sections/reference data to calculate total lift. */
export interface LiftContext {
  /** The picked balloon's envelope volume; `null` while no balloon is picked. */
  envelopeVolumeM3: number | null;
  /** The picked balloon's max envelope temperature (the calculator clamps it to 0-100 C); `null` without a balloon. */
  internalTemperatureC: number | null;
  siteElevationM: number;
  ambientTemperatureC: number;
}

export type { WeatherContext } from '../../../../core/flight-preparations/assessment-request';

const FEET_TO_METERS = 0.3048;

/** Section 05 — equipment weights, lift, the ordered passenger list and the live load verdict. */
@Component({
  selector: 'Nimbus-load-calculation-section',
  imports: [ReactiveFormsModule, FieldErrorPipe],
  templateUrl: './load-calculation-section.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['./load-calculation-section.css', '../section-fields.css'],
})
export class LoadCalculationSection {
  group = input.required<LoadCalculationFormGroup>();
  /** The pilot picked in section 01; their weight counts towards the total. */
  pilotId = input<string>('');

  /** Site elevation, balloon volume and take-off temperature for the total-lift calculation. */
  liftContext = input<LiftContext>({ envelopeVolumeM3: null, internalTemperatureC: null, siteElevationM: 0, ambientTemperatureC: 0 });

  /** Meteo values for the Go/No-Go verdict (section 02). */
  weather = input<WeatherContext>({ windSpeedKt: null, visibilityKm: null, capeJPerKg: null });

  private readonly liftApi = inject(LiftCalculationService);
  protected readonly calculatingLift = signal(false);
  protected readonly liftError = signal<string | null>(null);
  protected readonly canCalculateLift = computed(() => this.liftContext().envelopeVolumeM3 !== null && this.liftContext().internalTemperatureC !== null);

  private readonly fb = inject(NonNullableFormBuilder);
  private readonly pilotsStore = inject(PilotsStore);

  /** Re-emits on every change of the section's values (reactive forms are not signal-based). */
  private readonly values = toSignal(
    toObservable(this.group).pipe(switchMap((g) => g.valueChanges.pipe(startWith(null)))),
    { initialValue: null },
  );

  protected readonly pilotWeightKg = computed(() => {
    const id = this.pilotId();
    return Number(this.pilotsStore.list().find((p) => p.id === id)?.weightKg ?? 0);
  });

  private readonly assessmentApi = inject(FlightAssessmentService);

  /** The raw values sent to the server; this component never computes weight, lift sufficiency or the verdict itself. */
  private readonly assessmentRequest = computed<FlightAssessmentRequestDto>(() => {
    this.values();
    return buildAssessmentRequest(this.group().getRawValue(), this.pilotWeightKg(), this.weather());
  });

  /** The single server-side assessment (total weight, lift sufficiency, Go/No-Go verdict); `null` until it arrives or when it fails. */
  protected readonly assessment = toSignal(
    toObservable(this.assessmentRequest).pipe(
      debounceTime(150),
      switchMap((requestBody) => this.assessmentApi.postApiFlightAssessment({ requestBody }).pipe(catchError(() => of(null)))),
    ),
    { initialValue: null as FlightAssessmentDto | null },
  );

  protected get passengers() {
    return this.group().controls.passengers;
  }

  protected addPassenger(): void {
    this.passengers.push(createPassengerGroup(this.fb));
    this.group().markAsDirty();
  }

  protected removePassenger(index: number): void {
    this.passengers.removeAt(index);
    this.group().markAsDirty();
  }

  protected movePassenger(index: number, delta: -1 | 1): void {
    movePassenger(this.passengers, index, index + delta);
    this.group().markAsDirty();
  }

  /**
   * Prefills the total lift (max altitude is AMSL) from the Cameron manual formula (server side). The result lands in the
   * normal, still editable `totalLiftKg` control; nothing else is stored.
   */
  protected async calculateLift(): Promise<void> {
    const ctx = this.liftContext();
    if (ctx.envelopeVolumeM3 === null || ctx.internalTemperatureC === null || this.calculatingLift()) return;
    const v = this.group().getRawValue();
    this.calculatingLift.set(true);
    this.liftError.set(null);
    try {
      const result = await firstValueFrom(
        this.liftApi.getApiLiftCalculationTotalLift({
          maxAltitudeMeters: Math.round(Number(v.maxAltitudeFt) * FEET_TO_METERS * 10) / 10,
          siteElevationMeters: ctx.siteElevationM,
          ambientTemperatureC: ctx.ambientTemperatureC,
          internalTemperatureC: ctx.internalTemperatureC,
          envelopeVolumeM3: ctx.envelopeVolumeM3,
        }),
      );
      this.group().controls.totalLiftKg.setValue(Math.max(0, Number(result.totalLiftKg)));
      this.group().controls.totalLiftKg.markAsDirty();
    } catch {
      this.liftError.set('Could not calculate the lift. Check the inputs or enter it manually.');
    } finally {
      this.calculatingLift.set(false);
    }
  }
}
