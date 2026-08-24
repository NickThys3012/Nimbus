import { Component, input, output, ChangeDetectionStrategy } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';

@Component({
  selector: 'Nimbus-input-field',
  imports: [ReactiveFormsModule],
  templateUrl: './input-field.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './input-field.css',
})
export class InputField {
  label = input.required<string>();
  placeholder = input.required<string>();
  type = input.required<'text' | 'email'>();
  control = input<FormControl<string> | null>(null);
  showError = input(false);
  errorMessage = input('');

  value = input<string>('');
  valueChange = output<string>();

  onInput(event: Event) {
    const value = (event.target as HTMLInputElement | null)?.value ?? '';
    this.valueChange.emit(value);
  }
}
