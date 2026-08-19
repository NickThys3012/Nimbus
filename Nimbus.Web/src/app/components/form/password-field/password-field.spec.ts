import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PasswordField } from './password-field';

describe('PasswordField', () => {
  let component: PasswordField;
  let fixture: ComponentFixture<PasswordField>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PasswordField],
    }).compileComponents();

    fixture = TestBed.createComponent(PasswordField);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should emit value changes to the parent', () => {
    const emittedValues: string[] = [];
    component.valueChange.subscribe((value) => emittedValues.push(value));

    component.onInput({ target: { value: 'strong password' } } as unknown as Event);

    expect(emittedValues).toEqual(['strong password']);
  });
});
