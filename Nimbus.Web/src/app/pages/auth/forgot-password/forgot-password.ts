import { ChangeDetectionStrategy, Component, inject, OnDestroy, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Banner } from '../../../components/banner/banner';
import { Button } from '../../../components/button/button';
import { Glyph } from '../../../components/glyph/glyph';
import { InputField } from '../../../components/form/input-field/input-field';
import { PasswordResetService } from '../../../core/auth/password-reset.service';

const RESEND_COOLDOWN_SECONDS = 60;

@Component({
  selector: 'Nimbus-forgot-password',
  imports: [Banner, Button, Glyph, InputField, ReactiveFormsModule, RouterLink],
  templateUrl: './forgot-password.html',
  styleUrl: './forgot-password.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export default class ForgotPassword implements OnDestroy {
  readonly sent = signal(false);
  readonly busy = signal(false);
  readonly cooldown = signal(0);
  readonly submittedEmail = signal('');
  readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
  });
  private readonly reset = inject(PasswordResetService);
  private timer: ReturnType<typeof setInterval> | null = null;

  submit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.send(this.form.controls.email.value.trim());
  }

  resend() {
    if (this.cooldown() === 0) {
      this.send(this.submittedEmail());
    }
  }

  ngOnDestroy() {
    this.stopTimer();
  }

  private send(email: string) {
    this.busy.set(true);
    // The API answers identically for known and unknown addresses; so does the UI, errors included.
    const done = () => {
      this.busy.set(false);
      this.submittedEmail.set(email);
      this.sent.set(true);
      this.startCooldown();
    };
    this.reset.forgotPassword(email).subscribe({ next: done, error: done });
  }

  private startCooldown() {
    this.stopTimer();
    this.cooldown.set(RESEND_COOLDOWN_SECONDS);
    this.timer = setInterval(() => {
      this.cooldown.update((v) => v - 1);
      if (this.cooldown() <= 0) this.stopTimer();
    }, 1000);
  }

  private stopTimer() {
    if (this.timer) clearInterval(this.timer);
    this.timer = null;
  }
}
