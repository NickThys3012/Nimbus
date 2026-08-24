import { Component, input, output, ChangeDetectionStrategy, computed, signal, effect } from '@angular/core';
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

  value = input<string>('');
  valueChange = output<string>();

  // Signal to track control state changes
  controlStateVersion = signal(0);

  constructor() {
    effect(() => {
      const ctrl = this.control();
      if (!ctrl) return;

      const sub = ctrl.statusChanges.subscribe(() => {
        this.controlStateVersion.update(v => v + 1);
      });

      return () => sub.unsubscribe();
    });
  }

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
      return 'Use letters, numbers, and special characters.';
    }

    return 'Invalid value.';
  });

  onInput(event: Event) {
    const value = (event.target as HTMLInputElement | null)?.value ?? '';
    this.valueChange.emit(value);
  }
}
