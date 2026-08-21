import { Component, input, output, ChangeDetectionStrategy } from '@angular/core';

@Component({
  selector: 'Nimbus-input-field',
  imports: [],
  templateUrl: './input-field.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './input-field.css',
})
export class InputField {
  label = input.required<string>();
  placeholder = input.required<string>();
  type = input.required<'text' | 'email'>();

  value = input<string>('');
  valueChange = output<string>();

  onInput(event: Event) {
    const value = (event.target as HTMLInputElement | null)?.value ?? '';
    this.valueChange.emit(value);
  }
}
