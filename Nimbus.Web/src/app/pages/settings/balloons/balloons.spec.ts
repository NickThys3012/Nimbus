import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { type Mock, vi } from 'vitest';

import Balloons from './balloons';
import { ApiError, type BalloonDto, BalloonsService } from '../../../core/api-client';

const balloon: BalloonDto = {
  id: 'b1', registration: 'OO-BQZ', type: 'Cameron Z-105', envelopeVolumeM3: 3000, maxEnvelopeTemperatureC: 120,
  envelopeWeightKg: 135, basketWeightKg: 98, burnerWeightKg: 38, cylindersWeightKg: 72, emptyWeightKg: 343,
};
const other: BalloonDto = { ...balloon, id: 'b2', registration: 'OO-BLK', type: 'Cameron Z-90', emptyWeightKg: 325 };

function apiError(status: number, body: unknown): ApiError {
  return new ApiError(
    { method: 'POST', url: '/api/balloons' },
    { url: '/api/balloons', ok: false, status, statusText: '', body },
    'err',
  );
}

describe('Balloons', () => {
  let api: Record<string, Mock>;

  async function setup(list: BalloonDto[] = [balloon, other]) {
    api = {
      getApiBalloons: vi.fn().mockReturnValue(of(list)),
      getApiBalloonsUsage: vi.fn().mockReturnValue(of({ flightCount: 0, recentFlights: [] })),
      postApiBalloons: vi.fn().mockReturnValue(of(balloon)),
      putApiBalloons: vi.fn().mockReturnValue(of(balloon)),
      deleteApiBalloons: vi.fn().mockReturnValue(of(undefined)),
    };
    await TestBed.configureTestingModule({
      imports: [Balloons],
      providers: [{ provide: BalloonsService, useValue: api }],
    }).compileComponents();
    const fixture = TestBed.createComponent(Balloons);
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
    return { fixture, el, q, click, type };
  }

  async function fillValid(type: (id: string, v: string) => Promise<void>) {
    await type('registration', 'OO-NEW');
    await type('envelopeVolumeM3', '3000');
    await type('maxEnvelopeTemperatureC', '110');
    await type('envelopeWeightKg', '120');
    await type('basketWeightKg', '95');
    await type('burnerWeightKg', '38');
    await type('cylindersWeightKg', '72');
  }

  afterEach(() => vi.restoreAllMocks());

  it('lists balloons with subtitle, columns and footnote', async () => {
    const { el, q } = await setup();
    expect(q('subtitle').textContent).toContain('2 BALLOONS · ONLY VISIBLE TO YOU');
    expect(el.textContent).toContain('REG.');
    expect(el.textContent).toContain('MAX TEMP');
    expect(el.textContent).toContain('OO-BQZ');
    expect(el.textContent).toContain('3000 m³');
    expect(el.textContent).toContain('120 °C');
    expect(el.textContent).toContain('343 kg');
    expect(el.textContent).toContain('EMPTY WEIGHT = ENVELOPE + BASKET + BURNER + CYLINDERS · CALCULATED');
  });

  it('shows the empty state with an Add balloon button', async () => {
    const { el, q, click } = await setup([]);
    expect(el.textContent).toContain('No balloons yet');
    expect(el.textContent).toContain('instead of retyping its details');
    await click('add-balloon-empty');
    expect(q('balloon-form')).toBeTruthy();
  });

  it('shows the live formula and total, with no empty weight input', async () => {
    const { q, click, type, el } = await setup();
    await click('add-balloon');
    expect(el.querySelector('h1')?.textContent).toContain('Add balloon');
    await fillValid(type);
    expect(q('formula').textContent).toContain('120 + 95 + 38 + 72');
    expect(q('empty-weight').textContent).toContain('325 kg');
    expect(q('empty-weight-box').textContent).toContain('EMPTY WEIGHT · CALCULATED, READ-ONLY');
    expect(el.querySelector('[formcontrolname="emptyWeightKg"]')).toBeNull();
  });

  it('requires only the registration', async () => {
    const { q, click, type } = await setup();
    await click('add-balloon');
    await click('save-balloon');
    expect(api['postApiBalloons']).not.toHaveBeenCalled();
    expect(q('balloon-form').textContent).toContain('Registration is required.');

    await fillValid(type); // type stays empty
    await click('save-balloon');
    expect(api['postApiBalloons']).toHaveBeenCalledWith({
      requestBody: expect.objectContaining({ registration: 'OO-NEW', type: '', envelopeVolumeM3: 3000 }),
    });
    expect(api['getApiBalloons']).toHaveBeenCalledTimes(2);
  });

  it('edits an existing balloon with an "Edit OO-XXX" title', async () => {
    const { q, click, type, el } = await setup();
    await click('edit-b1');
    expect(el.querySelector('h1')?.textContent).toContain('Edit OO-BQZ');
    expect(q<HTMLInputElement>('registration').value).toBe('OO-BQZ');
    await type('type', 'Changed');
    await click('save-balloon');
    expect(api['putApiBalloons']).toHaveBeenCalledWith(
      expect.objectContaining({ id: 'b1', requestBody: expect.objectContaining({ type: 'Changed' }) }),
    );
  });

  it('shows the duplicate registration inline under the field on 409', async () => {
    const { q, click, type } = await setup();
    api['postApiBalloons'].mockReturnValue(throwError(() => apiError(409, { title: 'whatever' })));
    await click('add-balloon');
    await fillValid(type);
    await click('save-balloon');
    expect(q('duplicate-error').textContent).toContain('You already have a balloon with this registration.');
    expect(q('balloon-form')).toBeTruthy();
  });

  it('shows 422 validation messages', async () => {
    const { q, click, type } = await setup();
    api['postApiBalloons'].mockReturnValue(
      throwError(() => apiError(422, { title: 'Validation failed', errors: { Registration: ['Too long.'] } })),
    );
    await click('add-balloon');
    await fillValid(type);
    await click('save-balloon');
    expect(q('form-error').textContent).toContain('Too long.');
  });

  it('asks for confirmation when no flights use the balloon, then deletes', async () => {
    const { q, click, fixture } = await setup();
    await click('delete-b1');
    expect(api['getApiBalloonsUsage']).toHaveBeenCalledWith({ id: 'b1' });
    const dialog = q('confirm-dialog');
    expect(dialog.getAttribute('aria-modal')).toBe('true');
    expect(dialog.textContent).toContain('Delete OO-BQZ?');
    expect(dialog.textContent).toContain("Cameron Z-105 will be removed from your balloons. This can't be undone. No flights use this balloon.");
    expect(api['deleteApiBalloons']).not.toHaveBeenCalled();

    await click('dlg-cancel');
    expect(q('confirm-dialog')).toBeNull();
    expect(api['deleteApiBalloons']).not.toHaveBeenCalled();

    await click('delete-b1');
    await click('dlg-delete');
    expect(api['deleteApiBalloons']).toHaveBeenCalledWith({ id: 'b1' });
    expect(api['getApiBalloons']).toHaveBeenCalledTimes(2);
    expect(q('confirm-dialog')).toBeNull();
    void fixture;
  });

  it('closes a dialog with Escape', async () => {
    const { q, click, fixture } = await setup();
    await click('delete-b1');
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    await fixture.whenStable();
    expect(q('confirm-dialog')).toBeNull();
    expect(api['deleteApiBalloons']).not.toHaveBeenCalled();
  });

  it('shows the blocked dialog, and never deletes, when flights use the balloon', async () => {
    const { q, click } = await setup();
    api['getApiBalloonsUsage'].mockReturnValue(
      of({
        flightCount: 9,
        recentFlights: [
          { label: 'VR 08 · Weelde', date: '2026-10-08' },
          { label: 'ZO 27 · Kasterlee', date: '2026-09-27' },
        ],
      }),
    );
    await click('delete-b2');
    const dialog = q('blocked-dialog');
    expect(dialog.textContent).toContain("OO-BLK can't be deleted");
    expect(dialog.textContent).toContain('This balloon is used by 9 flights. Deleting it would break their logbook entries.');
    expect(dialog.textContent).toContain('VR 08 · Weelde');
    expect(dialog.textContent).toContain('08 OCT');
    expect(dialog.textContent).toContain('+ 7 MORE');
    expect(dialog.textContent).toContain('Edit the balloon instead if its details changed.');
    expect(q('dlg-delete')).toBeNull();

    await click('dlg-ok');
    expect(q('blocked-dialog')).toBeNull();
    expect(api['deleteApiBalloons']).not.toHaveBeenCalled();
  });

  it('surfaces a server 409 on delete', async () => {
    const { q, click } = await setup();
    api['deleteApiBalloons'].mockReturnValue(throwError(() => apiError(409, { title: 'This balloon is used by 1 flight.' })));
    await click('delete-b1');
    await click('dlg-delete');
    expect(q('list-error').textContent).toContain('used by 1 flight');
  });
});
