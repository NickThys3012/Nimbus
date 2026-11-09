import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';

import {
  type ChecklistsFormGroup,
  SAFETY_CHECK_KEYS,
  TECHNICAL_CHECK_KEYS,
} from '../../../../core/flight-preparations/flight-preparation-form';
import { CHECKLIST_ANSWERS, ChecklistAnswer } from '../../../../core/flight-preparations/flight-preparation.model';

/** Section 04 — safety checks (three-state: Yes / No / Not applicable) and technical checks (tick boxes). */
@Component({
  selector: 'Nimbus-checklists-section',
  imports: [ReactiveFormsModule],
  templateUrl: './checklists-section.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['./checklists-section.css', '../section-fields.css'],
})
export class ChecklistsSection {
  group = input.required<ChecklistsFormGroup>();

  protected readonly answers = CHECKLIST_ANSWERS;
  protected readonly answerLabels: Record<ChecklistAnswer, string> = {
    No: 'No',
    Yes: 'Yes',
    NotApplicable: 'N/A',
  };

  protected readonly safetyChecks = [
    { key: SAFETY_CHECK_KEYS[0], label: 'First-aid kit & fire extinguisher' },
    { key: SAFETY_CHECK_KEYS[1], label: 'Passenger list completed' },
    { key: SAFETY_CHECK_KEYS[2], label: 'Flight plan filed' },
  ] as const;

  protected readonly technicalChecks = [
    { key: TECHNICAL_CHECK_KEYS[0], label: 'Burner tested' },
    { key: TECHNICAL_CHECK_KEYS[1], label: 'Gas cylinders checked' },
    { key: TECHNICAL_CHECK_KEYS[2], label: 'Balloon visually inspected' },
    { key: TECHNICAL_CHECK_KEYS[3], label: 'Moorings checked' },
    { key: TECHNICAL_CHECK_KEYS[4], label: 'Instruments working' },
  ] as const;
}
