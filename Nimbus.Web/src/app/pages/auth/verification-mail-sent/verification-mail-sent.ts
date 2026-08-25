import { ChangeDetectionStrategy, Component, effect, inject, OnInit } from '@angular/core';
import { FormControl, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Button } from '../../../components/button/button';
import { InputField } from '../../../components/form/input-field/input-field';
import { Glyph } from '../../../components/glyph/glyph';
import { AuthStore } from '../../../core/auth/auth.store';
import { MatSnackBar } from '@angular/material/snack-bar';

@Component({
  selector: 'Nimbus-verification-mail-sent',
  imports: [Button, Glyph, InputField],
  templateUrl: './verification-mail-sent.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './verification-mail-sent.css',
})
export default class VerificationMailSent implements OnInit {
  sendAgain = false;
  emailAddress: string | null = null;
  resendCompleted = false;
  isEmailModalOpen = false;
  emailControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.email],
  });
  protected readonly authStore = inject(AuthStore);
  private lastShownError: string | null = null;
  private snackbar = inject(MatSnackBar);
  private route = inject(ActivatedRoute);

  constructor() {
    effect(() => {
      const error = this.authStore.error();

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

  ngOnInit() {
    this.route.queryParamMap.subscribe((params) => {
      this.sendAgain = params.get('sendAgain') === 'true';
      this.emailAddress = params.get('email') || null;
    });
  }

  openResendModal() {
    this.emailControl.setValue(this.emailAddress ?? '');
    this.emailControl.markAsPristine();
    this.emailControl.markAsUntouched();
    this.isEmailModalOpen = true;
  }

  closeResendModal() {
    this.isEmailModalOpen = false;
  }

  submitResendEmail() {
    if (this.emailControl.invalid) {
      this.emailControl.markAsTouched();
      return;
    }

    const email = this.emailControl.value.trim();

    this.authStore.requestNewVerificationEmail(email).subscribe({
      next: () => {
        this.emailAddress = email;
        this.sendAgain = true;
        this.resendCompleted = true;
        this.closeResendModal();
        this.snackbar.open('A new verification email has been sent.', 'Dismiss', {
          duration: 4500,
          horizontalPosition: 'end',
          verticalPosition: 'bottom',
        });
      },
    });
  }
}
