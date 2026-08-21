import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Button } from '../../../components/button/button';
import { Glyph } from '../../../components/glyph/glyph';
import { AuthStore } from '../../../core/auth/auth.store';

@Component({
  selector: 'Nimbus-verification-mail-sent',
  imports: [Button, Glyph],
  templateUrl: './verification-mail-sent.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './verification-mail-sent.css',
})
export default class VerificationMailSent {
  sendAgain = false;
  emailAddress: string | null = null;

  protected readonly authStore = inject(AuthStore);
  constructor(private route: ActivatedRoute) {}

  ngOnInit() {
    this.route.queryParamMap.subscribe((params) => {
      this.sendAgain = params.get('sendAgain') === 'true';
      this.emailAddress = params.get('email') || null;
    });
  }

  resendVerificationEmail() {
    if (this.emailAddress) {
      this.authStore.requestNewVerificationEmail(this.emailAddress);
      // show toast message to user that email has been sent
      console.log(this.authStore.error());
    }
  }
}
