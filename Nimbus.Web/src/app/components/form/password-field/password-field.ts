import { Component, computed, input, output, signal, ChangeDetectionStrategy } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';

@Component({
  selector: 'Nimbus-password-field',
  imports: [ReactiveFormsModule],
  templateUrl: './password-field.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './password-field.css',
})
export class PasswordField {
  placeholder = input.required<string>();
  control = input<FormControl<string> | null>(null);
  showError = input(false);
  errorMessage = input('');

  value = input<string>('');
  valueChange = output<string>();

  onInput(event: Event) {
    const value = (event.target as HTMLInputElement | null)?.value ?? '';
    this.valueChange.emit(value);
  }

  showPassword = signal(false);

  fieldType = computed(() => (this.showPassword() ? 'text' : 'password'));
}
