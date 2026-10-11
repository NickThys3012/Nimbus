import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';

import ResetPassword from './reset-password';
import { PasswordResetService } from '../../../core/auth/password-reset.service';

describe('ResetPassword', () => {
  const resetPassword = vi.fn();

  async function create(params: Record<string, string>) {
    resetPassword.mockReset();
    await TestBed.configureTestingModule({
      imports: [ResetPassword],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(params) } } },
        { provide: PasswordResetService, useValue: { resetPassword } },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(ResetPassword);
    fixture.detectChanges();
    return fixture;
  }

  it('shows the invalid-link state when the token is missing', async () => {
    const fixture = await create({ email: 'a@b.be' });
    expect((fixture.nativeElement as HTMLElement).querySelector('#reset-password-invalid')).not.toBeNull();
  });

  it('refuses mismatching passwords', async () => {
    const fixture = await create({ email: 'a@b.be', token: 't' });
    fixture.componentInstance.form.setValue({ password: 'Abcdef12', confirm: 'Abcdef13' });
    fixture.componentInstance.submit();
    expect(resetPassword).not.toHaveBeenCalled();
  });

  it('submits and shows the changed state', async () => {
    const fixture = await create({ email: 'a@b.be', token: 't' });
    resetPassword.mockReturnValue(of(void 0));
    fixture.componentInstance.form.setValue({ password: 'Abcdef12', confirm: 'Abcdef12' });
    fixture.componentInstance.submit();
    expect(resetPassword).toHaveBeenCalledWith('a@b.be', 't', 'Abcdef12');
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('#reset-password-changed')).not.toBeNull();
  });

  it('shows the invalid-link state when the API rejects the token', async () => {
    const fixture = await create({ email: 'a@b.be', token: 't' });
    resetPassword.mockReturnValue(throwError(() => new Error('400')));
    fixture.componentInstance.form.setValue({ password: 'Abcdef12', confirm: 'Abcdef12' });
    fixture.componentInstance.submit();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('#reset-password-invalid')).not.toBeNull();
  });
});
