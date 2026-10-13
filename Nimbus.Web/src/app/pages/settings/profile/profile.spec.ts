import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { type Mock, vi } from 'vitest';

import Profile from './profile';
import { ApiError, type ProfileDto, ProfileService } from '../../../core/api-client';
import { AuthStore } from '../../../core/auth/auth.store';

const empty: ProfileDto = { operatorName: null, picWeightKg: null };
const filled: ProfileDto = { operatorName: 'Skyline Ballooning', picWeightKg: 82 };

describe('Profile', () => {
  let api: Record<string, Mock>;

  async function setup(initial: ProfileDto = empty) {
    api = {
      getApiProfile: vi.fn().mockReturnValue(of(initial)),
      putApiProfile: vi.fn().mockReturnValue(of(filled)),
    };
    await TestBed.configureTestingModule({
      imports: [Profile],
      providers: [
        { provide: ProfileService, useValue: api },
        { provide: AuthStore, useValue: { email: signal('nick@example.be') } },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(Profile);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const q = <T extends HTMLElement>(id: string) => el.querySelector(`[data-testid="${id}"]`) as T;
    const type = async (id: string, value: string) => {
      const input = q<HTMLInputElement>(id);
      input.value = value;
      input.dispatchEvent(new Event('input'));
      await fixture.whenStable();
    };
    const saveBtn = () => q('save-profile').querySelector('button') as HTMLButtonElement;
    return { fixture, q, type, saveBtn };
  }

  afterEach(() => vi.restoreAllMocks());

  it('shows empty fields when the profile is not set yet', async () => {
    const { q, saveBtn } = await setup();
    expect(q('profile-email').textContent?.trim()).toBe('nick@example.be');
    expect(q<HTMLInputElement>('operatorName').value).toBe('');
    expect(q<HTMLInputElement>('picWeightKg').value).toBe('');
    expect(saveBtn().disabled).toBe(false);
  });

  it('shows the stored values', async () => {
    const { q } = await setup(filled);
    expect(q<HTMLInputElement>('operatorName').value).toBe('Skyline Ballooning');
    expect(q<HTMLInputElement>('picWeightKg').value).toBe('82');
  });

  it('saves the values and shows the saved notice', async () => {
    const { fixture, q, type, saveBtn } = await setup();
    await type('operatorName', '  Skyline Ballooning ');
    await type('picWeightKg', '82');
    saveBtn().click();
    await fixture.whenStable();
    expect(api['putApiProfile']).toHaveBeenCalledWith({ requestBody: { operatorName: 'Skyline Ballooning', picWeightKg: 82 } });
    expect(q('saved-notice')).toBeTruthy();
  });

  it('saves empty values as null (both optional)', async () => {
    const { fixture, type, saveBtn } = await setup(filled);
    await type('operatorName', '');
    await type('picWeightKg', '');
    saveBtn().click();
    await fixture.whenStable();
    expect(api['putApiProfile']).toHaveBeenCalledWith({ requestBody: { operatorName: null, picWeightKg: null } });
  });

  it('blocks a weight of 0 or less with an inline error and disables Save', async () => {
    const { q, type, saveBtn } = await setup();
    await type('picWeightKg', '0');
    expect(q('picWeightKg-error').textContent).toContain('Enter a weight above 0 kg.');
    expect(saveBtn().disabled).toBe(true);
    expect(api['putApiProfile']).not.toHaveBeenCalled();
  });

  it('shows a server 422 on the weight field', async () => {
    const { fixture, q, type, saveBtn } = await setup();
    api['putApiProfile'].mockReturnValue(
      throwError(() => new ApiError({ method: 'PUT', url: '/x' }, { url: '/x', ok: false, status: 422, statusText: '', body: { errors: { 'Profile.PicWeightKg': ['Enter a weight above 0 kg.'] } } }, 'err')),
    );
    await type('picWeightKg', '80');
    saveBtn().click();
    await fixture.whenStable();
    expect(q('picWeightKg-error').textContent).toContain('Enter a weight above 0 kg.');
  });
});
