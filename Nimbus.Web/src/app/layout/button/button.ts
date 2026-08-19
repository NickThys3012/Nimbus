import { Component, input } from '@angular/core';

@Component({
  selector: 'Nimbus-button',
  imports: [],
  templateUrl: './button.html',
  styleUrl: './button.css',
})
export class Button {
  id = input.required<string>();
  label = input.required<string>();
  icon = input<string>();
}
