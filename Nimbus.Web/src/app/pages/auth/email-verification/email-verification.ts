import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { FormControl, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Glyph } from '../../../components/glyph/glyph';
import { Button } from '../../../components/button/button';
import { InputField } from '../../../components/form/input-field/input-field';
import { AuthStore } from '../../../core/auth/auth.store';
import { MatSnackBar } from '@angular/material/snack-bar';

type VerificationStatus = 'success' | 'error';

@Component({
  selector: 'Nimbus-email-verification',
  imports: [Glyph, Button, InputField],
  templateUrl: './email-verification.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './email-verification.css',
})
export default class EmailVerification {
  status = signal<VerificationStatus>('error');
  isResendModalOpen = false;
  emailControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.email],
  });
  private lastShownError: string | null = null;
  private readonly activatedRoute = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly authStore = inject(AuthStore);
  private readonly snackbar = inject(MatSnackBar);

  constructor() {
    const status = this.activatedRoute.snapshot.queryParamMap.get('status');
    const email = this.activatedRoute.snapshot.queryParamMap.get('email');

    if (status === 'success') {
      this.status.set('success');
    }

    if (email) {
      this.emailControl.setValue(email);
    }

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

  goToLogin(): void {
    void this.router.navigate(['/login']);
  }

  openResendModal(): void {
    this.emailControl.markAsPristine();
    this.emailControl.markAsUntouched();
    this.isResendModalOpen = true;
  }

  closeResendModal(): void {
    this.isResendModalOpen = false;
  }

  submitResendEmail(): void {
    if (this.emailControl.invalid) {
      this.emailControl.markAsTouched();
      return;
    }

    const email = this.emailControl.value.trim();
    this.authStore.requestNewVerificationEmail(email).subscribe({
      next: () => {
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
