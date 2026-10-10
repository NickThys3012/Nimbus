import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';

import { CountBadge } from '../count-badge/count-badge';

export interface TabItem {
  id: string;
  label: string;
  /** Optional count badge next to the label. */
  count?: number;
  countTone?: 'neutral' | 'warn';
}

@Component({
  selector: 'Nimbus-tabs',
  imports: [CountBadge],
  templateUrl: './tabs.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './tabs.css',
})
export class Tabs {
  tabs = input.required<TabItem[]>();
  /** Id of the active tab (two-way bindable). */
  active = model.required<string>();
}
