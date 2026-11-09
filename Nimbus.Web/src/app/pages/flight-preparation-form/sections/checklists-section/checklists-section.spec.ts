import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder } from '@angular/forms';

import { createFlightPreparationForm } from '../../../../core/flight-preparations/flight-preparation-form';
import { ChecklistAnswer } from '../../../../core/flight-preparations/flight-preparation.model';
import { ChecklistsSection } from './checklists-section';

describe('ChecklistsSection', () => {
  let fixture: ComponentFixture<ChecklistsSection>;
  let group: ReturnType<typeof createFlightPreparationForm>['controls']['checklists'];

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [ChecklistsSection] });
    group = createFlightPreparationForm(TestBed.inject(NonNullableFormBuilder)).controls.checklists;
    fixture = TestBed.createComponent(ChecklistsSection);
    fixture.componentRef.setInput('group', group);
    fixture.detectChanges();
  });

  const el = (id: string) => fixture.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLInputElement;

  it('renders nothing pre-ticked: safety defaults to No, technical checks unchecked', () => {
    expect(el('firstAidAndExtinguisher-No').checked).toBe(true);
    expect(el('flightPlanFiled-Yes').checked).toBe(false);
    expect(el('burnerTested').checked).toBe(false);
  });

  it('offers a not-applicable option for each safety check and none for technical checks', () => {
    expect(el('passengerListFilled-NotApplicable')).toBeTruthy();
    expect(el('burnerTested-NotApplicable')).toBeNull();
  });

  it('writes the typed enum value to the form when choosing an answer', () => {
    el('passengerListFilled-NotApplicable').click();
    el('mooringsChecked').click();
    expect(group.controls.passengerListFilled.value).toBe(ChecklistAnswer.NotApplicable);
    expect(group.controls.mooringsChecked.value).toBe(true);
  });
});
