import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';

import { type ChecklistsFormGroup } from '../../../../core/flight-preparations/flight-preparation-form';

/** Section 04 — a simple checklist; completion is "all items checked". */
@Component({
  selector: 'Nimbus-checklists-section',
  imports: [ReactiveFormsModule],
  templateUrl: './checklists-section.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['./checklists-section.css', '../section-fields.css'],
})
export class ChecklistsSection {
  group = input.required<ChecklistsFormGroup>();

  protected get items() {
    return this.group().controls.items;
  }
}
