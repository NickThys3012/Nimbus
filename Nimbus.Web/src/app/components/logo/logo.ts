import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

@Component({
  selector: 'Nimbus-logo',
  templateUrl: './logo.html',
  styleUrl: './logo.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[style.--logo-tile-size]': 'tileSize()',
    '[style.--logo-svg-size]': 'svgSize()',
  },
})
export class Logo {
  /** 'sm' = 28px (navbar default), 'lg' = 44px (auth page) */
  size = input<'sm' | 'lg'>('sm');

  tileSize = computed(() => (this.size() === 'lg' ? '44px' : '28px'));
  svgSize = computed(() => (this.size() === 'lg' ? '34px' : '22px'));
}
