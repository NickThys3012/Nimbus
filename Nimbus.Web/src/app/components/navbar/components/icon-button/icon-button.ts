import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

@Component({
  selector: 'Nimbus-icon-button',
  imports: [],
  templateUrl: './icon-button.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './icon-button.css',
})
export class IconButton {
  icon = input.required<string>();
  ariaLabel = input.required<string>();
  id = input<string>();
  ariaControls = input<string>();
  ariaExpanded = input<boolean | null>(null);
  disabled = input<boolean>(false);
  pressed = output<void>();

  protected onPressed(): void {
    if (!this.disabled()) {
      this.pressed.emit();
    }
  }
}

