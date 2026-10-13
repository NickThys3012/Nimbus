import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { type Mock, vi } from 'vitest';

import GoNoGo from './go-no-go';
import { ApiError, type GoNoGoThresholdsDto, GoNoGoThresholdsService } from '../../../core/api-client';

const defaults: GoNoGoThresholdsDto = { windYellowKt: 10, windRedKt: 15, visibilityYellowKm: 5, visibilityRedKm: 3, capeYellow: 300, capeRed: 500, isCustom: false };
const custom: GoNoGoThresholdsDto = { windYellowKt: 8, windRedKt: 12, visibilityYellowKm: 6, visibilityRedKm: 4, capeYellow: 250, capeRed: 400, isCustom: true };

describe('GoNoGo', () => {
  let api: Record<string, Mock>;

  async function setup(initial: GoNoGoThresholdsDto = defaults) {
    api = {
      getApiGoNoGoThresholds: vi.fn().mockReturnValue(of(initial)),
      putApiGoNoGoThresholds: vi.fn().mockReturnValue(of(custom)),
      deleteApiGoNoGoThresholds: vi.fn().mockReturnValue(of(defaults)),
    };
    await TestBed.configureTestingModule({
      imports: [GoNoGo],
      providers: [{ provide: GoNoGoThresholdsService, useValue: api }],
    }).compileComponents();
    const fixture = TestBed.createComponent(GoNoGo);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const q = <T extends HTMLElement>(id: string) => el.querySelector(`[data-testid="${id}"]`) as T;
    const click = async (id: string) => {
      const target = q(id);
      (target.querySelector('button') ?? target).click();
      await fixture.whenStable();
    };
    const type = async (id: string, value: string) => {
      const input = q<HTMLInputElement>(id);
      input.value = value;
      input.dispatchEvent(new Event('input'));
      await fixture.whenStable();
    };
    const saveBtn = () => q('save-thresholds').querySelector('button') as HTMLButtonElement;
    return { fixture, el, q, click, type, saveBtn };
  }

  afterEach(() => vi.restoreAllMocks());

  it('shows the defaults with the USING DEFAULTS badge', async () => {
    const { q } = await setup();
    expect(q('mode-badge').textContent?.trim()).toBe('USING DEFAULTS');
    expect(q<HTMLInputElement>('windYellowKt').value).toBe('10');
    expect(q<HTMLInputElement>('windRedKt').value).toBe('15');
    expect(q<HTMLInputElement>('visibilityYellowKm').value).toBe('5');
    expect(q<HTMLInputElement>('visibilityRedKm').value).toBe('3');
    expect(q<HTMLInputElement>('capeYellow').value).toBe('300');
    expect(q<HTMLInputElement>('capeRed').value).toBe('500');
    expect(q('preview-chip').textContent?.trim()).toBe('CAU');
  });

  it('shows custom values with the CUSTOM badge and a NO-GO preview', async () => {
    const { q } = await setup(custom);
    expect(q('mode-badge').textContent?.trim()).toBe('CUSTOM');
    expect(q<HTMLInputElement>('windRedKt').value).toBe('12');
    expect(q('preview-chip').textContent?.trim()).toBe('NO-GO');
  });

  it('refuses inconsistent input: field error, banner, save disabled', async () => {
    const { q, type, saveBtn, click } = await setup();
    await type('windRedKt', '10');
    expect(q('windRedKt-error').textContent).toContain('Must be higher than 10 kt.');
    await type('windRedKt', '9');
    expect(q('windRedKt-error').textContent).toContain('Must be higher than 10 kt.');
    await type('visibilityRedKm', '5');
    expect(q('visibilityRedKm-error').textContent).toContain('Must be lower than 5 km.');
    expect(q('inconsistent-banner').textContent).toContain('2 thresholds are inconsistent');
    expect(saveBtn().disabled).toBe(true);
    await click('save-thresholds');
    expect(api['putApiGoNoGoThresholds']).not.toHaveBeenCalled();
  });

  it('refuses empty and non-positive values', async () => {
    const { q, type, saveBtn } = await setup();
    await type('capeYellow', '');
    expect(q('capeYellow-error')).toBeTruthy();
    await type('capeYellow', '0');
    expect(saveBtn().disabled).toBe(true);
  });

  it('saves the numbers and confirms', async () => {
    const { q, type, click } = await setup();
    await type('windYellowKt', '8');
    await click('save-thresholds');
    expect(api['putApiGoNoGoThresholds']).toHaveBeenCalledWith({
      requestBody: { windYellowKt: 8, windRedKt: 15, visibilityYellowKm: 5, visibilityRedKm: 3, capeYellow: 300, capeRed: 500 },
    });
    expect(q('saved-notice').textContent).toContain('Thresholds saved');
    expect(q('mode-badge').textContent?.trim()).toBe('CUSTOM');
    expect(q<HTMLInputElement>('windRedKt').value).toBe('12');
  });

  it('shows server-side validation under the field', async () => {
    const { q, type, click } = await setup();
    api['putApiGoNoGoThresholds'].mockReturnValue(
      throwError(() => new ApiError({ method: 'PUT', url: '/x' }, { url: '/x', ok: false, status: 422, statusText: '', body: { errors: { 'Thresholds.CapeRed': ['Too big.'] } } }, 'e')),
    );
    await type('capeYellow', '310');
    await click('save-thresholds');
    expect(q('capeRed-error').textContent).toContain('Too big.');
    expect(q('saved-notice')).toBeNull();
  });

  it('asks for confirmation before resetting, and Cancel changes nothing', async () => {
    const { el, q, click } = await setup(custom);
    await click('reset-thresholds');
    expect(el.querySelector('[role="alertdialog"]')).toBeTruthy();
    expect(q('reset-defaults').textContent).toContain('10 / 15 kt');
    expect(q('reset-defaults').textContent).toContain('5 / 3 km');
    expect(q('reset-defaults').textContent).toContain('300 / 500 J/kg');
    await click('reset-cancel');
    expect(api['deleteApiGoNoGoThresholds']).not.toHaveBeenCalled();
    expect(q('mode-badge').textContent?.trim()).toBe('CUSTOM');
  });

  it('resets to defaults after confirming', async () => {
    const { q, click } = await setup(custom);
    await click('reset-thresholds');
    await click('reset-confirm');
    expect(api['deleteApiGoNoGoThresholds']).toHaveBeenCalledOnce();
    expect(q('mode-badge').textContent?.trim()).toBe('USING DEFAULTS');
    expect(q<HTMLInputElement>('windRedKt').value).toBe('15');
  });
});
