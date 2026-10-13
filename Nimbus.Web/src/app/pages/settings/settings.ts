import { ChangeDetectionStrategy, Component, signal } from '@angular/core';

import Balloons from './balloons/balloons';
import GoNoGo from './go-no-go/go-no-go';
import LaunchLocations from './launch-locations/launch-locations';
import Pilots from './pilots/pilots';
import Profile from './profile/profile';
import { type TabItem, Tabs } from '../../components/tabs/tabs';
import Ofp from './ofp/ofp';

type SettingsTab =
  'profile' | 'balloons' | 'pilots' | 'launch-locations' | 'go-no-go' | 'ofp' | 'preferences';

@Component({
  selector: 'Nimbus-settings',
  imports: [Balloons, GoNoGo, LaunchLocations, Pilots, Profile, Tabs, Ofp],
  templateUrl: './settings.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './settings.css',
})
export default class Settings {
  protected readonly tab = signal<SettingsTab>('balloons');
  protected readonly tabs: TabItem[] = [
    { id: 'profile', label: 'Profile' },
    { id: 'balloons', label: 'Balloons' },
    { id: 'pilots', label: 'Pilots' },
    { id: 'launch-locations', label: 'Locations' },
    { id: 'go-no-go', label: 'Go/No-Go' },
    { id: 'ofp', label: 'OFP' },
    { id: 'preferences', label: 'Preferences' },
  ];

  protected setTab(id: string): void {
    this.tab.set(id as SettingsTab);
  }
}
