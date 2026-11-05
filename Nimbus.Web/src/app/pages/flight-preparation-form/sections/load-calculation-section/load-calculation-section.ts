import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';

import { createPassengerGroup, type LoadCalculationFormGroup } from '../../../../core/flight-preparations/flight-preparation-form';
import { FieldErrorPipe } from '../field-error.pipe';

/** Section 05 — envelope weight, max altitude, and the passenger list. */
@Component({
  selector: 'Nimbus-load-calculation-section',
  imports: [ReactiveFormsModule, FieldErrorPipe],
  templateUrl: './load-calculation-section.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['./load-calculation-section.css', '../section-fields.css'],
})
export class LoadCalculationSection {
  formGroup = input.required<LoadCalculationFormGroup>();

  private readonly fb = inject(NonNullableFormBuilder);

  protected get passengers() {
    return this.formGroup().controls.passengers;
  }

  protected readonly totalWeightKg = () =>
    this.passengers.controls.reduce((sum, p) => sum + Number(p.controls.weightKg.value || 0), 0);

  protected addPassenger(): void {
    this.passengers.push(createPassengerGroup(this.fb));
  }

  protected removePassenger(index: number): void {
    this.passengers.removeAt(index);
  }
}
