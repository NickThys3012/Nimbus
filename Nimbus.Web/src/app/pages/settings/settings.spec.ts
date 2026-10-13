import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import Settings from './settings';
import {
  BalloonsService,
  GoNoGoThresholdsService,
  LaunchLocationsService,
  OfpSettingsService,
  PilotsService,
  ProfileService,
} from '../../core/api-client';
import { AuthStore } from '../../core/auth/auth.store';

describe('Settings', () => {
  async function setup() {
    await TestBed.configureTestingModule({
      imports: [Settings],
      providers: [
        { provide: BalloonsService, useValue: { getApiBalloons: () => of([]) } },
        { provide: PilotsService, useValue: { getApiPilots: () => of([]) } },
        { provide: LaunchLocationsService, useValue: { getApiLaunchLocations: () => of([]) } },
        { provide: AuthStore, useValue: { email: signal('pilot@example.be') } },
        { provide: ProfileService, useValue: { getApiProfile: () => of({ operatorName: null, picWeightKg: null }) } },
        { provide: OfpSettingsService, useValue: { getApiOfpSettings: () => of({ equipmentWeightPerPassengerKg: 7, isCustom: false }) } },
        { provide: GoNoGoThresholdsService, useValue: { getApiGoNoGoThresholds: () => of({ windYellowKt: 10, windRedKt: 15, visibilityYellowKm: 5, visibilityRedKm: 3, capeYellow: 300, capeRed: 500, isCustom: false }) } },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(Settings);
    await fixture.whenStable();
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  it('shows Profile / Balloons / Pilots / Locations / Go/No-Go / OFP / Preferences tabs with Balloons active', async () => {
    const { el } = await setup();
    const tabs = Array.from(el.querySelectorAll('[role="tab"]')).map((t) => t.textContent?.trim());
    expect(tabs).toEqual(['Profile', 'Balloons', 'Pilots', 'Locations', 'Go/No-Go', 'OFP', 'Preferences']);
    expect(el.querySelector('#tab-balloons')?.getAttribute('aria-selected')).toBe('true');
    expect(el.querySelector('Nimbus-balloons')).toBeTruthy();
  });

  it('shows the profile page on the Profile tab', async () => {
    const { fixture, el } = await setup();
    (el.querySelector('#tab-profile') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(el.querySelector('Nimbus-profile')).toBeTruthy();
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

  it('shows the Go/No-Go page on the Go/No-Go tab', async () => {
    const { fixture, el } = await setup();
    (el.querySelector('#tab-go-no-go') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(el.querySelector('Nimbus-go-no-go')).toBeTruthy();
    expect(el.querySelector('Nimbus-balloons')).toBeNull();
  });

  it('shows the OFP page on the OFP tab', async () => {
    const { fixture, el } = await setup();
    (el.querySelector('#tab-ofp') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(el.querySelector('Nimbus-ofp')).toBeTruthy();
    expect(el.querySelector('Nimbus-balloons')).toBeNull();
  });
});
