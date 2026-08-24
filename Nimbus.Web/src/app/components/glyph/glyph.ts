import { ChangeDetectionStrategy, Component, input } from '@angular/core';

type GlyphVariant = 'Primary' | 'Ok' | 'Warning' | 'Error';

@Component({
  selector: 'Nimbus-glyph',
  imports: [],
  templateUrl: './glyph.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './glyph.css',
})
export class Glyph {
  icon = input.required<string>();
  variant = input<GlyphVariant>('Primary');
}
