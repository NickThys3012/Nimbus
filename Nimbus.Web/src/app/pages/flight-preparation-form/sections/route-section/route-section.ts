import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';

import { type RouteFormGroup } from '../../../../core/flight-preparations/flight-preparation-form';
import { FieldErrorPipe } from '../field-error.pipe';

/** Section 06 — route description, climb/descent rates, cruise altitudes. */
@Component({
  selector: 'Nimbus-route-section',
  imports: [ReactiveFormsModule, FieldErrorPipe],
  templateUrl: './route-section.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['./route-section.css', '../section-fields.css'],
})
export class RouteSection {
  formGroup = input.required<RouteFormGroup>();
}
