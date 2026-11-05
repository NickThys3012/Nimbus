import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';

import { type OfpFormGroup } from '../../../../core/flight-preparations/flight-preparation-form';

/** Section 08 — operational flight plan reference fields. */
@Component({
  selector: 'Nimbus-ofp-section',
  imports: [ReactiveFormsModule],
  templateUrl: './ofp-section.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['./ofp-section.css', '../section-fields.css'],
})
export class OfpSection {
  formGroup = input.required<OfpFormGroup>();
}
