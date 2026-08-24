import {
  Component,
  inject,
  ChangeDetectionStrategy,
  effect,
  OnInit,
} from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Button } from '../../../components/button/button';
import { Glyph } from '../../../components/glyph/glyph';
import { AuthStore } from '../../../core/auth/auth.store';
import { MatSnackBar } from '@angular/material/snack-bar';


@Component({
  selector: 'Nimbus-verification-mail-sent',
  imports: [Button, Glyph],
  templateUrl: './verification-mail-sent.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './verification-mail-sent.css',
})
export default class VerificationMailSent implements OnInit {
  sendAgain = false;
  emailAddress: string | null = null;
  private lastShownError: string | null = null;
  private snackbar = inject(MatSnackBar);
  private route = inject(ActivatedRoute);

  protected readonly authStore = inject(AuthStore);
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

  resendVerificationEmail() {
    if (this.emailAddress) {
      this.authStore.requestNewVerificationEmail(this.emailAddress);
    }
  }
}
