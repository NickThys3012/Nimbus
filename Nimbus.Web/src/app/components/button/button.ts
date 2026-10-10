import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';

type ButtonVariant = 'primary' | 'ghost' | 'approve' | 'danger';
type ButtonSize = 'md' | 'sm';

@Component({
  selector: 'Nimbus-button',
  imports: [NgTemplateOutlet],
  templateUrl: './button.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './button.css',
})
export class Button {
  id = input.required<string>();
  icon = input<string>();
  type = input<'button' | 'submit' | 'reset'>('button');
  /** When set, renders an anchor (e.g. `mailto:`) with the same look instead of a button. */
  href = input<string>();
  size = input<ButtonSize>('md');
  /** Accessible name for icon-only buttons. */
  ariaLabel = input<string>();
  variant = input<ButtonVariant>('primary');
  disabled = input<boolean>(false);
  fullWidth = input<boolean>(true);
}
