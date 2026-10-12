import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { type Mock, vi } from 'vitest';

import Pilots from './pilots';
import { ApiError, type PilotDto, PilotsService } from '../../../core/api-client';

const nick: PilotDto = { id: 'p1', name: 'N. Thys', weightKg: 82 };
const kris: PilotDto = { id: 'p2', name: 'K. Van Dyck', weightKg: 76 };

describe('Pilots', () => {
  let api: Record<string, Mock>;

  async function setup(list: PilotDto[] = [nick, kris]) {
    api = {
      getApiPilots: vi.fn().mockReturnValue(of(list)),
      postApiPilots: vi.fn().mockReturnValue(of(nick)),
      putApiPilots: vi.fn().mockReturnValue(of(nick)),
      deleteApiPilots: vi.fn().mockReturnValue(of(undefined)),
    };
    await TestBed.configureTestingModule({
      imports: [Pilots],
      providers: [{ provide: PilotsService, useValue: api }],
    }).compileComponents();
    const fixture = TestBed.createComponent(Pilots);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const q = <T extends HTMLElement>(id: string) => el.querySelector(`[data-testid="${id}"]`) as T;
    const dialog = () => el.querySelector('[role="alertdialog"]') as HTMLElement | null;
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
    return { fixture, el, q, dialog, click, type };
  }

  afterEach(() => vi.restoreAllMocks());

  it('lists pilots with their weights, in the order the API returns', async () => {
    const { el } = await setup();
    const names = Array.from(el.querySelectorAll('.nm')).map((n) => n.textContent?.trim());
    expect(names).toEqual(['N. Thys', 'K. Van Dyck']);
    expect(el.textContent).toContain('82 kg');
    expect(el.textContent).toContain('76 kg');
  });

  it('shows an empty state with an Add pilot button', async () => {
    const { el, q, click } = await setup([]);
    expect(el.textContent).toContain('No pilots yet');
    await click('add-pilot-empty');
    expect(q('pilot-form')).toBeTruthy();
  });

  it('validates name and the weight range client side', async () => {
    const { q, click, type } = await setup();
    await click('add-pilot');
    await click('save-pilot');
    expect(api['postApiPilots']).not.toHaveBeenCalled();
    expect(q('pilot-form').textContent).toContain('Name is required.');

    await type('name', 'New');
    await type('weightKg', '5');
    await click('save-pilot');
    expect(api['postApiPilots']).not.toHaveBeenCalled();
    expect(q('weight-error').textContent).toContain('between 20 and 250');
  });

  it('creates a pilot with a trimmed name and reloads the list', async () => {
    const { click, type } = await setup();
    await click('add-pilot');
    await type('name', '  New Pilot ');
    await type('weightKg', '70.5');
    await click('save-pilot');
    expect(api['postApiPilots']).toHaveBeenCalledWith({ requestBody: { name: 'New Pilot', weightKg: 70.5 } });
    expect(api['getApiPilots']).toHaveBeenCalledTimes(2);
  });

  it('edits an existing pilot', async () => {
    const { q, click, type, el } = await setup();
    await click('edit-p1');
    expect(el.querySelector('h1')?.textContent).toContain('Edit N. Thys');
    expect(q<HTMLInputElement>('name').value).toBe('N. Thys');
    await type('weightKg', '85');
    await click('save-pilot');
    expect(api['putApiPilots']).toHaveBeenCalledWith({ id: 'p1', requestBody: { name: 'N. Thys', weightKg: 85 } });
  });

  it('shows 422 messages from the server', async () => {
    const { q, click, type } = await setup();
    api['postApiPilots'].mockReturnValue(
      throwError(() => new ApiError({ method: 'POST', url: '/x' }, { url: '/x', ok: false, status: 422, statusText: '', body: { errors: { WeightKg: ['Nope.'] } } }, 'err')),
    );
    await click('add-pilot');
    await type('name', 'X');
    await type('weightKg', '70');
    await click('save-pilot');
    expect(q('form-error').textContent).toContain('Nope.');
  });

  it('confirms before deleting, and cancel leaves the pilot alone', async () => {
    const { click, dialog } = await setup();
    await click('delete-p1');
    expect(dialog()?.textContent).toContain('Delete N. Thys?');
    expect(api['deleteApiPilots']).not.toHaveBeenCalled();

    await click('dlg-cancel');
    expect(dialog()).toBeNull();
    expect(api['deleteApiPilots']).not.toHaveBeenCalled();

    await click('delete-p1');
    await click('dlg-delete');
    expect(api['deleteApiPilots']).toHaveBeenCalledWith({ id: 'p1' });
    expect(dialog()).toBeNull();
  });
});
