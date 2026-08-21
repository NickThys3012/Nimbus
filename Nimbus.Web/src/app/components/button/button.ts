import { Component, input } from '@angular/core';

type ButtonVariant = 'primary' | 'ghost';

@Component({
  selector: 'Nimbus-button',
  imports: [],
  templateUrl: './button.html',
  styleUrl: './button.css',
})
export class Button {
  id = input.required<string>();
  icon = input<string>();
  variant = input<ButtonVariant>('primary');
  disabled = input<boolean>(false);
}
