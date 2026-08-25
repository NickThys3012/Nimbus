import { Component, effect, inject, signal } from '@angular/core';
import { InputField } from '../../../../../components/form/input-field/input-field';
import { PasswordField } from '../../../../../components/form/password-field/password-field';
import { Button } from '../../../../../components/button/button';
import { Banner } from '../../../../../components/banner/banner';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthStore } from '../../../../../core/auth/auth.store';
import { Router } from '@angular/router';

@Component({
  imports: [InputField, PasswordField, Button, Banner, ReactiveFormsModule],
  selector: 'Nimbus-login',
  styleUrl: './login.css',
  templateUrl: './login.html',
})
export class Login {
  hasError = signal(false);
  private fb = inject(NonNullableFormBuilder);
  loginForm = this.fb.group({
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

  constructor() {
    effect(() => {
      const error = this.authClient.error();
      this.hasError.set(!!error);
    });
  }

  protected onSubmit() {
    if (!this.loginForm.valid) {
      this.loginForm.markAllAsTouched();
      return;
    }
    const { email, password } = this.loginForm.value;

    this.authClient
      .login({
        email: email!,
        password: password!,
      })
      .subscribe({
        next: () => this.router.navigate(['/home'], { queryParams: { email: email } }),
      });
  }
}
