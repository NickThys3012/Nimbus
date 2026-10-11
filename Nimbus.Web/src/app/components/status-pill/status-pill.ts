import { ChangeDetectionStrategy, Component, input } from '@angular/core';

type PillTone = 'ok' | 'warn' | 'danger';

@Component({
  selector: 'Nimbus-status-pill',
  imports: [],
  templateUrl: './status-pill.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './status-pill.css',
})
export class StatusPill {
  tone = input<PillTone>('ok');
}
