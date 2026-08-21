import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Banner } from '../../../components/banner/banner';
import { Button } from '../../../components/button/button';

type VerificationStatus = 'success' | 'error';

@Component({
  selector: 'Nimbus-email-verification',
  imports: [Banner, Button, RouterLink],
  templateUrl: './email-verification.html',
  styleUrl: './email-verification.css',
})
export default class EmailVerification {
  private readonly activatedRoute = inject(ActivatedRoute);
  private readonly router = inject(Router);

  status = signal<VerificationStatus>('error');
  isSuccess = computed(() => this.status() === 'success');

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

