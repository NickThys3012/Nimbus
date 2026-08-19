import { Component, computed, input, output, signal } from '@angular/core';

@Component({
  selector: 'Nimbus-password-field',
  imports: [],
  templateUrl: './password-field.html',
  styleUrl: './password-field.css',
})
export class PasswordField {
  placeholder = input.required<string>();

  value = input<string>('');
  valueChange = output<string>();

  onInput(event: Event) {
    const value = (event.target as HTMLInputElement | null)?.value ?? '';
    this.valueChange.emit(value);
  }

  showPassword = signal(false);

  fieldType = computed(() => (this.showPassword() ? 'text' : 'password'));
}
