import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

@Component({
  selector: 'Nimbus-banner',
  imports: [],
  templateUrl: './banner.html',
  styleUrl: './banner.css',
  changeDetection: ChangeDetectionStrategy.Eager,
  host: {
    '[style.--background-color]': 'backgroundColor()',
    '[style.--color]': 'color()',
  },
})
export class Banner {
  id = input.required<string>();
  icon = input<string>();
  type = input<'info' | 'warning'>('info');

  backgroundColor = computed(() => (this.type() === 'info' ? 'var(--acbg)' : 'var(--ab)'));
  color = computed(() => (this.type() === 'info' ? 'var(--ac)' : 'var(--a)'));
}
