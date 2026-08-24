import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Glyph } from '../../../components/glyph/glyph';
import { Button } from '../../../components/button/button';

type VerificationStatus = 'success' | 'error';

@Component({
  selector: 'Nimbus-email-verification',
  imports: [Glyph, Button],
  templateUrl: './email-verification.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './email-verification.css',
})
export default class EmailVerification {
  status = signal<VerificationStatus>('error');
  private readonly activatedRoute = inject(ActivatedRoute);
  private readonly router = inject(Router);

  constructor() {
    const status = this.activatedRoute.snapshot.queryParamMap.get('status');

    if (status === 'success') {
      this.status.set('success');
    }
  }

  goToLogin(): void {
    void this.router.navigate(['/login']);
  }
}
