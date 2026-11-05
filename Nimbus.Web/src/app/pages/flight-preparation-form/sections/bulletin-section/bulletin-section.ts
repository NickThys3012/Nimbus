import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';

import { type BulletinFormGroup } from '../../../../core/flight-preparations/flight-preparation-form';

/** Section 07 — free-text balloon bulletin. */
@Component({
  selector: 'Nimbus-bulletin-section',
  imports: [ReactiveFormsModule],
  templateUrl: './bulletin-section.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['./bulletin-section.css', '../section-fields.css'],
})
export class BulletinSection {
  formGroup = input.required<BulletinFormGroup>();
}
