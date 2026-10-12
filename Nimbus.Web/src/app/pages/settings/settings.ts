import { ChangeDetectionStrategy, Component, signal } from '@angular/core';

import Balloons from './balloons/balloons';
import Pilots from './pilots/pilots';
import { type TabItem, Tabs } from '../../components/tabs/tabs';

type SettingsTab = 'profile' | 'balloons' | 'pilots' | 'preferences';

@Component({
  selector: 'Nimbus-settings',
  imports: [Balloons, Pilots, Tabs],
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
    { id: 'preferences', label: 'Preferences' },
  ];

  protected setTab(id: string): void {
    this.tab.set(id as SettingsTab);
  }
}
