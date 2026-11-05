import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';

import { type GeneralFormGroup } from '../../../../core/flight-preparations/flight-preparation-form';
import { BalloonsStore } from '../../../../core/balloons/balloons.store';
import { LaunchLocationsStore } from '../../../../core/launch-locations/launch-locations.store';
import { PilotsStore } from '../../../../core/pilots/pilots.store';
import { FieldErrorPipe } from '../field-error.pipe';

/** Section 01 — flight date/time, balloon/pilot/location pickers, sunrise/sunset. */
@Component({
  selector: 'Nimbus-general-section',
  imports: [ReactiveFormsModule, FieldErrorPipe],
  templateUrl: './general-section.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['./general-section.css', '../section-fields.css'],
})
export class GeneralSection {
  formGroup = input.required<GeneralFormGroup>();

  private readonly balloonsStore = inject(BalloonsStore);
  private readonly pilotsStore = inject(PilotsStore);
  private readonly locationsStore = inject(LaunchLocationsStore);

  protected readonly balloons = this.balloonsStore.list;
  protected readonly pilots = this.pilotsStore.list;
  protected readonly locations = this.locationsStore.list;
}
