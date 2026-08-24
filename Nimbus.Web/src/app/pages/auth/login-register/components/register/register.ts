import { ChangeDetectionStrategy, Component, effect, inject } from '@angular/core';
import { Button } from '../../../../../components/button/button';
import { Banner } from '../../../../../components/banner/banner';
import { InputField } from '../../../../../components/form/input-field/input-field';
import { PasswordField } from '../../../../../components/form/password-field/password-field';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthStore } from '../../../../../core/auth/auth.store';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';

@Component({
  selector: 'Nimbus-register',
  imports: [Button, Banner, InputField, PasswordField, ReactiveFormsModule],
  templateUrl: './register.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './register.css',
})
export class Register {
  private fb = inject(NonNullableFormBuilder);
  registerForm = this.fb.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    password: [
      '',
      [
        Validators.required,
        Validators.minLength(8),
        Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^A-Za-z0-9]).{8,}$/),
      ],
    ],
  });
  private authClient = inject(AuthStore);
  private router = inject(Router);
  private snackbar = inject(MatSnackBar);
  private lastShownError: string | null = null;

  constructor() {
    effect(() => {
      const error = this.authClient.error();

      if (error && error !== this.lastShownError) {
        this.snackbar.open(error, 'Dismiss', {
          duration: 6500,
          horizontalPosition: 'end',
          verticalPosition: 'bottom',
          panelClass: ['nimbus-snackbar-error'],
        });
        this.lastShownError = error;
      }

      if (!error) {
        this.lastShownError = null;
      }
    });
  }

  protected onSubmit() {
    if (!this.registerForm.valid) {
      this.registerForm.markAllAsTouched();
      return;
    }
    const { firstName, lastName, email, password } = this.registerForm.value;

    this.authClient
      .register({
        firstName: firstName!,
        lastName: lastName!,
        email: email!,
        password: password!,
      })
      .subscribe({
        next: () =>
          this.router.navigate(['/verification-email-sent'], { queryParams: { email: email } }),
      });
  }
}
