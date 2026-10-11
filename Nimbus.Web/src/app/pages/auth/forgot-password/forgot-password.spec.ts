import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';

import ForgotPassword from './forgot-password';
import { PasswordResetService } from '../../../core/auth/password-reset.service';

describe('ForgotPassword', () => {
  const forgotPassword = vi.fn();

  async function create() {
    forgotPassword.mockReset();
    forgotPassword.mockReturnValue(of(void 0));
    await TestBed.configureTestingModule({
      imports: [ForgotPassword],
      providers: [provideRouter([]), { provide: PasswordResetService, useValue: { forgotPassword } }],
    }).compileComponents();
    const fixture = TestBed.createComponent(ForgotPassword);
    fixture.detectChanges();
    return fixture;
  }

  it('does not submit an invalid address', async () => {
    const fixture = await create();
    fixture.componentInstance.submit();
    expect(forgotPassword).not.toHaveBeenCalled();
  });

  it('shows the same confirmation after submitting', async () => {
    const fixture = await create();
    fixture.componentInstance.form.controls.email.setValue('pilot@example.com');
    fixture.componentInstance.submit();
    fixture.detectChanges();
    expect(forgotPassword).toHaveBeenCalledWith('pilot@example.com');
    expect((fixture.nativeElement as HTMLElement).querySelector('#forgot-password-sent')).not.toBeNull();
    expect(fixture.componentInstance.cooldown()).toBe(60);
    fixture.destroy();
  });
});
