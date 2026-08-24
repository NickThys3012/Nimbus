import { ChangeDetectionStrategy, Component, input } from '@angular/core';

type ButtonVariant = 'primary' | 'ghost';

@Component({
  selector: 'Nimbus-button',
  imports: [],
  templateUrl: './button.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './button.css',
})
export class Button {
  id = input.required<string>();
  icon = input<string>();
  type = input.required<'button' | 'submit' | 'reset'>();
  variant = input<ButtonVariant>('primary');
  disabled = input<boolean>(false);
}
