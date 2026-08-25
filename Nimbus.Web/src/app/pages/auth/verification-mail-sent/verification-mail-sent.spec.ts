import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { convertToParamMap, ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { MatSnackBar } from '@angular/material/snack-bar';
import { vi } from 'vitest';

import VerificationMailSent from './verification-mail-sent';
import { AuthStore } from '../../../core/auth/auth.store';

describe('VerificationMailSent', () => {
  let component: VerificationMailSent;
  let fixture: ComponentFixture<VerificationMailSent>;
  const requestNewVerificationEmail = vi.fn();
  const snackBarOpen = vi.fn();

  beforeEach(async () => {
    vi.clearAllMocks();
    requestNewVerificationEmail.mockReturnValue(of(void 0));

    const authStoreMock = {
      error: signal<string | null>(null).asReadonly(),
      isLoading: signal(false).asReadonly(),
      requestNewVerificationEmail,
    };

    await TestBed.configureTestingModule({
      imports: [VerificationMailSent],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            queryParamMap: of(
              convertToParamMap({
                email: 'user@example.com',
                sendAgain: 'true',
              }),
            ),
          },
        },
        { provide: AuthStore, useValue: authStoreMock },
        { provide: MatSnackBar, useValue: { open: snackBarOpen } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(VerificationMailSent);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should read resend state and email from query params', () => {
    expect(component.sendAgain).toBe(true);
    expect(component.emailAddress).toBe('user@example.com');
  });

  it('should open the resend modal with prefilled email', () => {
    component.openResendModal();

    expect(component.isEmailModalOpen).toBe(true);
    expect(component.emailControl.value).toBe('user@example.com');
  });

  it('should request a new verification email and show success feedback', () => {
    component.openResendModal();
    component.emailControl.setValue('updated@example.com');

    component.submitResendEmail();

    expect(requestNewVerificationEmail).toHaveBeenCalledWith('updated@example.com');
    expect(component.isEmailModalOpen).toBe(false);
    expect(component.emailAddress).toBe('updated@example.com');
    expect(component.resendCompleted).toBe(true);
    expect(snackBarOpen).toHaveBeenCalledWith(
      'A new verification email has been sent.',
      'Dismiss',
      expect.objectContaining({ duration: 4500 }),
    );
  });

  it('should keep modal open and not submit when email is invalid', () => {
    component.openResendModal();
    component.emailControl.setValue('invalid-email');

    component.submitResendEmail();

    expect(requestNewVerificationEmail).not.toHaveBeenCalled();
    expect(component.isEmailModalOpen).toBe(true);
    expect(component.emailControl.touched).toBe(true);
  });
});
