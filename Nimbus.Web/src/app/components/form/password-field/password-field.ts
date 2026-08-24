import { ChangeDetectionStrategy, Component, computed, effect, input, output, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';

@Component({
  selector: 'Nimbus-password-field',
  imports: [ReactiveFormsModule],
  templateUrl: './password-field.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './password-field.css',
})
export class PasswordField {
  placeholder = input.required<string>();
  control = input<FormControl<string> | null>(null);

  value = input<string>('');
  valueChange = output<string>();

  // Signal to track control state changes
  controlStateVersion = signal(0);
  showError = computed(() => {
    // Access controlStateVersion to make this reactive to control changes
    this.controlStateVersion();
    const control = this.control();
    return control?.invalid && (control?.touched || control?.dirty);
  });
  errorMessage = computed(() => {
    // Access controlStateVersion to make this reactive to control changes
    this.controlStateVersion();
    const control = this.control();
    if (!control?.errors) {
      return '';
    }

    if (control.errors['required']) {
      return 'This field is required.';
    }
    if (control.errors['email']) {
      return 'Enter a valid email address.';
    }
    if (control.errors['minlength']) {
      return `Use at least ${control.errors['minlength'].requiredLength} characters.`;
    }
    if (control.errors['pattern']) {
      return 'Use letters and numbers.';
    }

    return 'Invalid value.';
  });
  showPassword = signal(false);
  fieldType = computed(() => (this.showPassword() ? 'text' : 'password'));

  constructor() {
    effect(() => {
      const ctrl = this.control();
      if (!ctrl) return;

      const sub = ctrl.statusChanges.subscribe(() => {
        this.controlStateVersion.update((v) => v + 1);
      });

      return () => sub.unsubscribe();
    });
  }

  onInput(event: Event) {
    const value = (event.target as HTMLInputElement | null)?.value ?? '';
    this.valueChange.emit(value);
  }
}
