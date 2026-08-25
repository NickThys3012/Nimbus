import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';
import { vi } from 'vitest';

import EmailVerification from './email-verification';
import { AuthStore } from '../../../core/auth/auth.store';

describe('EmailVerification', () => {
  const requestNewVerificationEmail = vi.fn();
  const snackBarOpen = vi.fn();

  async function createComponent(status: 'success' | 'error') {
    vi.clearAllMocks();
    requestNewVerificationEmail.mockReturnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [EmailVerification],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: convertToParamMap({ status, email: 'user@example.com' }),
            },
          },
        },
        {
          provide: AuthStore,
          useValue: {
            error: signal<string | null>(null).asReadonly(),
            isLoading: signal(false).asReadonly(),
            requestNewVerificationEmail,
          },
        },
        { provide: MatSnackBar, useValue: { open: snackBarOpen } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(EmailVerification);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('should create', async () => {
    const fixture = await createComponent('success');
    const component = fixture.componentInstance;
    expect(component).toBeTruthy();
  });

  it('renders success message when status=success', async () => {
    const fixture: ComponentFixture<EmailVerification> = await createComponent('success');
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('#verification-success')).not.toBeNull();
    expect(element.querySelector('#verification-error')).toBeNull();
    expect(element.querySelector('#resend-verification-link')).toBeNull();
  });

  it('renders error message and resend CTA when status=error', async () => {
    const fixture: ComponentFixture<EmailVerification> = await createComponent('error');
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('#verification-success')).toBeNull();
    expect(element.querySelector('#verification-error')).not.toBeNull();
    expect(element.querySelector('#resend-verification-link')).not.toBeNull();
  });

  it('opens modal and submits resend email', async () => {
    const fixture: ComponentFixture<EmailVerification> = await createComponent('error');
    const component = fixture.componentInstance;

    component.openResendModal();
    component.emailControl.setValue('new@example.com');
    component.submitResendEmail();

    expect(component.isResendModalOpen).toBe(false);
    expect(requestNewVerificationEmail).toHaveBeenCalledWith('new@example.com');
    expect(snackBarOpen).toHaveBeenCalledWith(
      'A new verification email has been sent.',
      'Dismiss',
      expect.objectContaining({ duration: 4500 }),
    );
  });

  it('keeps modal open on invalid email', async () => {
    const fixture: ComponentFixture<EmailVerification> = await createComponent('error');
    const component = fixture.componentInstance;

    component.openResendModal();
    component.emailControl.setValue('invalid');
    component.submitResendEmail();

    expect(component.isResendModalOpen).toBe(true);
    expect(component.emailControl.touched).toBe(true);
    expect(requestNewVerificationEmail).not.toHaveBeenCalled();
  });
});

