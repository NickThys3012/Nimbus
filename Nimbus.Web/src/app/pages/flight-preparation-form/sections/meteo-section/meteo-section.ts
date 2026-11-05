import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';

import { createWindLevelGroup, type MeteoFormGroup } from '../../../../core/flight-preparations/flight-preparation-form';

/** Section 02 — wind/visibility/METAR-TAF plus the wind-on-altitude table. */
@Component({
  selector: 'Nimbus-meteo-section',
  imports: [ReactiveFormsModule],
  templateUrl: './meteo-section.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['./meteo-section.css', '../section-fields.css'],
})
export class MeteoSection {
  formGroup = input.required<MeteoFormGroup>();

  private readonly fb = inject(NonNullableFormBuilder);

  protected get windLevels() {
    return this.formGroup().controls.windLevels;
  }

  protected addWindLevel(): void {
    this.windLevels.push(createWindLevelGroup(this.fb));
  }

  protected removeWindLevel(index: number): void {
    this.windLevels.removeAt(index);
  }
}
