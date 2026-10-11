import { ChangeDetectionStrategy, Component, ElementRef, inject, input, model } from '@angular/core';

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
  /** Accessible name of the tablist. */
  label = input<string>('Sections');

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  /**
   * Keyboard support of the ARIA tabs pattern with automatic activation: Left/Right move (and wrap),
   * Home/End jump to the first/last tab. Only the active tab is in the tab order (roving tabindex).
   */
  protected onKeydown(event: KeyboardEvent): void {
    const tabs = this.tabs();
    const current = tabs.findIndex((t) => t.id === this.active());
    let next: number;
    switch (event.key) {
      case 'ArrowRight':
        next = (current + 1) % tabs.length;
        break;
      case 'ArrowLeft':
        next = (current - 1 + tabs.length) % tabs.length;
        break;
      case 'Home':
        next = 0;
        break;
      case 'End':
        next = tabs.length - 1;
        break;
      default:
        return;
    }
    event.preventDefault();
    const id = tabs[next].id;
    this.active.set(id);
    this.host.nativeElement.querySelector<HTMLElement>(`#tab-${id}`)?.focus();
  }
}
