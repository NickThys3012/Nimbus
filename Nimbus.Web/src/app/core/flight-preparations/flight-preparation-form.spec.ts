import { FormBuilder } from '@angular/forms';

import {
  checklistProgress,
  createFlightPreparationForm,
  nextFlightSlot,
  SAFETY_CHECK_KEYS,
  TECHNICAL_CHECK_KEYS,
} from './flight-preparation-form';
import { ChecklistAnswer } from './flight-preparation.model';

describe('nextFlightSlot', () => {
  it('picks the evening slot when it is morning before the evening window', () => {
    expect(nextFlightSlot(new Date(2026, 7, 8, 9, 30))).toEqual({ flightDate: '2026-08-08', flightTime: '19:00' });
  });

  it('picks the morning slot of the same day when it is early enough', () => {
    expect(nextFlightSlot(new Date(2026, 7, 8, 5, 0))).toEqual({ flightDate: '2026-08-08', flightTime: '07:00' });
  });

  it('rolls over to tomorrow morning late in the evening, including month ends', () => {
    expect(nextFlightSlot(new Date(2026, 7, 31, 19, 30))).toEqual({ flightDate: '2026-09-01', flightTime: '07:00' });
  });

  it('requires at least an hour of lead time', () => {
    expect(nextFlightSlot(new Date(2026, 7, 8, 6, 30))).toEqual({ flightDate: '2026-08-08', flightTime: '19:00' });
  });
});

describe('checklists (issue #34)', () => {
  const form = () => createFlightPreparationForm(new FormBuilder().nonNullable);

  it('defaults every safety check to No and every technical check to unchecked', () => {
    const value = form().getRawValue().checklists;
    for (const key of SAFETY_CHECK_KEYS) expect(value[key]).toBe(ChecklistAnswer.No);
    for (const key of TECHNICAL_CHECK_KEYS) expect(value[key]).toBe(false);
    expect(form().getRawValue().airspace.notamsChecked).toBe(false);
  });

  it('requires NOTAMs to be checked before the airspace section validates', () => {
    const f = form();
    expect(f.controls.airspace.valid).toBe(false);
    f.controls.airspace.controls.notamsChecked.setValue(true);
    expect(f.controls.airspace.valid).toBe(true);
  });

  it('counts Yes and Not applicable as done, but not No', () => {
    const f = form();
    f.controls.checklists.patchValue({
      firstAidAndExtinguisher: ChecklistAnswer.Yes,
      flightPlanFiled: ChecklistAnswer.NotApplicable,
      burnerTested: true,
    });
    expect(checklistProgress(f.getRawValue().checklists)).toEqual({ done: 3, total: 8 });
  });
});
