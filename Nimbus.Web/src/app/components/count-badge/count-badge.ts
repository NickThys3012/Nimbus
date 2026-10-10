import { ChangeDetectionStrategy, Component, input } from '@angular/core';

type BadgeTone = 'neutral' | 'warn';

@Component({
  selector: 'Nimbus-count-badge',
  imports: [],
  templateUrl: './count-badge.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './count-badge.css',
})
export class CountBadge {
  count = input.required<number>();
  tone = input<BadgeTone>('neutral');
}
