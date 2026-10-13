import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import Settings from './settings';
import { BalloonsService, LaunchLocationsService, PilotsService } from '../../core/api-client';

describe('Settings', () => {
  async function setup() {
    await TestBed.configureTestingModule({
      imports: [Settings],
      providers: [
        { provide: BalloonsService, useValue: { getApiBalloons: () => of([]) } },
        { provide: PilotsService, useValue: { getApiPilots: () => of([]) } },
        { provide: LaunchLocationsService, useValue: { getApiLaunchLocations: () => of([]) } },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(Settings);
    await fixture.whenStable();
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  it('shows Profile / Balloons / Pilots / Launch locations / Preferences tabs with Balloons active', async () => {
    const { el } = await setup();
    const tabs = Array.from(el.querySelectorAll('[role="tab"]')).map((t) => t.textContent?.trim());
    expect(tabs).toEqual(['Profile', 'Balloons', 'Pilots', 'Launch locations', 'Preferences']);
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

  it('shows the pilots page on the Pilots tab', async () => {
    const { fixture, el } = await setup();
    (el.querySelector('#tab-pilots') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(el.querySelector('Nimbus-pilots')).toBeTruthy();
    expect(el.querySelector('Nimbus-balloons')).toBeNull();
  });

  it('shows the launch locations page on the Launch locations tab', async () => {
    const { fixture, el } = await setup();
    (el.querySelector('#tab-launch-locations') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(el.querySelector('Nimbus-launch-locations')).toBeTruthy();
    expect(el.querySelector('Nimbus-balloons')).toBeNull();
  });
});
