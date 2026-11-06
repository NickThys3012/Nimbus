import { ChangeDetectionStrategy, Component, computed, input, model } from '@angular/core';

import { type SectionCompletionStatus } from '../../core/flight-preparations/flight-preparation.model';

export type { SectionCompletionStatus };

/**
 * One accordion panel for the flight-preparation form (issue #32): a header showing the section
 * number, a completion dot, an optional live summary, and a chevron, plus a body that's only
 * rendered while expanded. Independently expandable — each instance owns its own `expanded` model,
 * so several sections can be open at once.
 */
@Component({
  selector: 'Nimbus-section-card',
  imports: [],
  templateUrl: './section-card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './section-card.css',
  host: {
    '[attr.data-section]': 'sectionId()',
  },
})
export class SectionCard {
  sectionId = input.required<string>();
  order = input.required<number>();
  title = input.required<string>();
  status = input<SectionCompletionStatus>('pending');
  /** Short live summary shown next to the title while collapsed, e.g. "6 kt · 240°". */
  summary = input<string | null>(null);
  /** `'edited'` shows a "● EDITED" badge, `'from-draft'` a "● FROM DRAFT" one (issue #32 mockup
   *  frames 1B/3A); `null` shows neither. */
  badge = input<'edited' | 'from-draft' | null>(null);
  expanded = model(false);

  protected readonly orderLabel = computed(() => this.order().toString().padStart(2, '0'));
  protected readonly bodyId = computed(() => `fp-section-body-${this.sectionId()}`);

  protected toggle(): void {
    this.expanded.update((v) => !v);
  }
}
