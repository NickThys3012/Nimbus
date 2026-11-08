import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { startWith, switchMap } from 'rxjs';

import {
  createPassengerGroup,
  type LoadCalculationFormGroup,
  movePassenger,
} from '../../../../core/flight-preparations/flight-preparation-form';
import { calculateLoad } from '../../../../core/flight-preparations/load-calculation';
import { PilotsStore } from '../../../../core/pilots/pilots.store';
import { FieldErrorPipe } from '../field-error.pipe';

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

  /** Derived on every change; never written back to the form. */
  protected readonly load = computed(() => {
    this.values();
    const v = this.group().getRawValue();
    return calculateLoad({ ...v, pilotWeightKg: this.pilotWeightKg(), passengerWeightsKg: v.passengers.map((p) => p.weightKg) });
  });

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
}
