import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';

import { type AirspaceFormGroup } from '../../../../core/flight-preparations/flight-preparation-form';

/** Section 03 — NOTAMs checked + free-text airspace structure/restrictions/obstacles notes. */
@Component({
  selector: 'Nimbus-airspace-section',
  imports: [ReactiveFormsModule],
  templateUrl: './airspace-section.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['./airspace-section.css', '../section-fields.css'],
})
export class AirspaceSection {
  formGroup = input.required<AirspaceFormGroup>();
}
