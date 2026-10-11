import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'Nimbus-empty-state',
  imports: [],
  templateUrl: './empty-state.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './empty-state.css',
})
export class EmptyState {
  icon = input.required<string>();
}
