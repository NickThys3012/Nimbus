import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'Nimbus-root',
  imports: [RouterOutlet],
  templateUrl: './Nimbus.html',
  styleUrl: './Nimbus.css',
})
export class Nimbus {
  protected readonly title = signal('Nimbus.Web');
}
