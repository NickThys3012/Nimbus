import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import Settings from './settings';
import { BalloonsService } from '../../core/api-client';

describe('Settings', () => {
  async function setup() {
    await TestBed.configureTestingModule({
      imports: [Settings],
      providers: [{ provide: BalloonsService, useValue: { getApiBalloons: () => of([]) } }],
    }).compileComponents();
    const fixture = TestBed.createComponent(Settings);
    await fixture.whenStable();
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  it('shows Profile / Balloons / Preferences tabs with Balloons active', async () => {
    const { el } = await setup();
    const tabs = Array.from(el.querySelectorAll('[role="tab"]')).map((t) => t.textContent?.trim());
    expect(tabs).toEqual(['Profile', 'Balloons', 'Preferences']);
    expect(el.querySelector('#tab-balloons')?.getAttribute('aria-selected')).toBe('true');
    expect(el.querySelector('Nimbus-balloons')).toBeTruthy();
  });

  it('switches to the placeholder tabs', async () => {
    const { fixture, el } = await setup();
    (el.querySelector('#tab-profile') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(el.querySelector('[data-testid="profile-placeholder"]')).toBeTruthy();
    expect(el.querySelector('Nimbus-balloons')).toBeNull();
  });
});
