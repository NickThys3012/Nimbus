import { ComponentFixture, TestBed } from '@angular/core/testing';

import { InputField } from './input-field';

describe('InputField', () => {
  let component: InputField;
  let fixture: ComponentFixture<InputField>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [InputField],
    }).compileComponents();

    fixture = TestBed.createComponent(InputField);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('label', 'Test label');
    fixture.componentRef.setInput('placeholder', 'Test placeholder');
    fixture.componentRef.setInput('type', 'text');
    fixture.detectChanges();
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should emit value changes to the parent', () => {
    const emittedValues: string[] = [];
    component.valueChange.subscribe((value) => emittedValues.push(value));

    component.onInput({ target: { value: 'hello@example.com' } } as unknown as Event);

    expect(emittedValues).toEqual(['hello@example.com']);
  });
});
