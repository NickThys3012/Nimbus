import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Banner } from '../../../components/banner/banner';
import { Button } from '../../../components/button/button';
import { Glyph } from '../../../components/glyph/glyph';
import { PasswordField } from '../../../components/form/password-field/password-field';
import { PasswordStrength } from '../../../components/password-strength/password-strength';
import { PasswordResetService } from '../../../core/auth/password-reset.service';

const passwordsMatch = (group: AbstractControl): ValidationErrors | null =>
  group.get('password')?.value === group.get('confirm')?.value ? null : { mismatch: true };

@Component({
  selector: 'Nimbus-reset-password',
  imports: [Banner, DatePipe, Button, Glyph, PasswordField, PasswordStrength, ReactiveFormsModule, RouterLink],
  templateUrl: './reset-password.html',
  styleUrl: './reset-password.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export default class ResetPassword {
  private readonly route = inject(ActivatedRoute);
  private readonly reset = inject(PasswordResetService);

  readonly email = this.route.snapshot.queryParamMap.get('email') ?? '';
  readonly token = this.route.snapshot.queryParamMap.get('token') ?? '';
  readonly state = signal<'form' | 'invalid' | 'changed'>(!this.email || !this.token ? 'invalid' : 'form');
  readonly busy = signal(false);
  readonly changedAt = signal<Date | null>(null);
  readonly newPassword = signal('');

  readonly form = inject(NonNullableFormBuilder).group(
    {
      password: ['', [Validators.required, Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9]).{8,}$/)]],
      confirm: ['', [Validators.required]],
    },
    { validators: passwordsMatch },
  );

  constructor() {
    this.form.controls.password.valueChanges.subscribe((v) => this.newPassword.set(v));
  }

  submit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.busy.set(true);
    this.reset.resetPassword(this.email, this.token, this.form.controls.password.value).subscribe({
      next: () => {
        this.changedAt.set(new Date());
        this.state.set('changed');
      },
      error: () => {
        this.busy.set(false);
        this.state.set('invalid');
      },
    });
  }
}
