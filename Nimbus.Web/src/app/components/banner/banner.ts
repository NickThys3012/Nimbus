import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

@Component({
  selector: 'Nimbus-banner',
  imports: [],
  templateUrl: './banner.html',
  styleUrl: './banner.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[style.--background-color]': 'backgroundColor()',
    '[style.--color]': 'color()',
  },
})
export class Banner {
  id = input.required<string>();
  icon = input<string>();
  type = input<'info' | 'warning' | 'error'>('info');

  backgroundColor = computed(() => {
    const t = this.type();
    if (t === 'info') return 'var(--acbg)';
    return t === 'error' ? 'var(--rb)' : 'var(--ab)';
  });
  color = computed(() => {
    const t = this.type();
    if (t === 'info') return 'var(--ac)';
    return t === 'error' ? 'var(--r)' : 'var(--a)';
  });
}
