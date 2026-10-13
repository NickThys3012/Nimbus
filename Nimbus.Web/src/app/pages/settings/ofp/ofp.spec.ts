import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { type Mock, vi } from 'vitest';

import Ofp from './ofp';
import { ApiError, type OfpSettingsDto, OfpSettingsService } from '../../../core/api-client';

const defaults: OfpSettingsDto = { equipmentWeightPerPassengerKg: 7, isCustom: false };
const custom: OfpSettingsDto = { equipmentWeightPerPassengerKg: 9, isCustom: true };

describe('Ofp settings', () => {
  let api: Record<string, Mock>;

  async function setup(initial: OfpSettingsDto = defaults) {
    api = {
      getApiOfpSettings: vi.fn().mockReturnValue(of(initial)),
      putApiOfpSettings: vi.fn().mockReturnValue(of(custom)),
      deleteApiOfpSettings: vi.fn().mockReturnValue(of(defaults)),
    };
    await TestBed.configureTestingModule({
      imports: [Ofp],
      providers: [{ provide: OfpSettingsService, useValue: api }],
    }).compileComponents();
    const fixture = TestBed.createComponent(Ofp);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const q = <T extends HTMLElement>(id: string) => el.querySelector(`[data-testid="${id}"]`) as T;
    const click = async (id: string) => {
      const target = q(id);
      (target.querySelector('button') ?? target).click();
      await fixture.whenStable();
    };
    const type = async (value: string) => {
      const input = q<HTMLInputElement>('weight');
      input.value = value;
      input.dispatchEvent(new Event('input'));
      await fixture.whenStable();
    };
    const saveBtn = () => q('save-ofp').querySelector('button') as HTMLButtonElement;
    return { el, q, click, type, saveBtn };
  }

  afterEach(() => vi.restoreAllMocks());

  it('shows the default 7 kg with the DEFAULT badge and the worked example', async () => {
    const { q } = await setup();
    expect(q('mode-badge').textContent?.trim()).toBe('DEFAULT');
    expect(q<HTMLInputElement>('weight').value).toBe('7');
    expect(q('example-total').textContent?.trim()).toBe('56 kg');
  });

  it('shows the CUSTOM badge for a saved value', async () => {
    const { q } = await setup(custom);
    expect(q('mode-badge').textContent?.trim()).toBe('CUSTOM');
    expect(q<HTMLInputElement>('weight').value).toBe('9');
  });

  it('shows the unsaved change, the new example total and the flown-flights notice', async () => {
    const { q, type } = await setup();
    await type('9');
    expect(q('mode-badge').textContent).toContain('UNSAVED · 7 → 9 KG');
    expect(q('example-total').textContent?.trim()).toBe('72 kg');
    expect(q('example').textContent).toContain('WAS 56 KG');
    expect(q('flown-notice').textContent).toContain('Already-flown flights keep the OFP figures');
  });

  it('refuses negative, empty and too fine values', async () => {
    const { q, type, saveBtn, click } = await setup();
    await type('-3');
    expect(q('weight-error').textContent).toContain('Enter a weight of 0 kg or more.');
    expect(q('example-invalid')).toBeTruthy();
    expect(saveBtn().disabled).toBe(true);
    await type('');
    expect(q('weight-error')).toBeTruthy();
    await type('1.234');
    expect(q('weight-error').textContent).toContain('2 decimal');
    await click('save-ofp');
    expect(api['putApiOfpSettings']).not.toHaveBeenCalled();
  });

  it('accepts 0 kg', async () => {
    const { q, type, saveBtn } = await setup();
    await type('0');
    expect(q('weight-error')).toBeNull();
    expect(saveBtn().disabled).toBe(false);
  });

  it('saves the number and confirms', async () => {
    const { q, type, click } = await setup();
    await type('9');
    await click('save-ofp');
    expect(api['putApiOfpSettings']).toHaveBeenCalledWith({ requestBody: { equipmentWeightPerPassengerKg: 9 } });
    expect(q('saved-notice').textContent).toContain('OFP settings saved');
    expect(q('mode-badge').textContent?.trim()).toBe('CUSTOM');
  });

  it('shows server-side validation under the field', async () => {
    const { q, type, click } = await setup();
    api['putApiOfpSettings'].mockReturnValue(
      throwError(() => new ApiError({ method: 'PUT', url: '/x' }, { url: '/x', ok: false, status: 422, statusText: '', body: { errors: { 'Settings.EquipmentWeightPerPassengerKg': ['Too big.'] } } }, 'e')),
    );
    await type('9');
    await click('save-ofp');
    expect(q('weight-error').textContent).toContain('Too big.');
    expect(q('saved-notice')).toBeNull();
  });

  it('asks for confirmation before resetting, and Cancel changes nothing', async () => {
    const { el, q, click } = await setup(custom);
    await click('reset-ofp');
    expect(el.querySelector('[role="alertdialog"]')).toBeTruthy();
    await click('reset-cancel');
    expect(api['deleteApiOfpSettings']).not.toHaveBeenCalled();
    expect(q('mode-badge').textContent?.trim()).toBe('CUSTOM');
  });

  it('resets to the default after confirming', async () => {
    const { q, click } = await setup(custom);
    await click('reset-ofp');
    await click('reset-confirm');
    expect(api['deleteApiOfpSettings']).toHaveBeenCalledOnce();
    expect(q('mode-badge').textContent?.trim()).toBe('DEFAULT');
    expect(q<HTMLInputElement>('weight').value).toBe('7');
  });
});
