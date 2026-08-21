import { Component, ChangeDetectionStrategy } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Navbar } from './components/navbar/navbar';

@Component({
  selector: 'Nimbus-root',
  imports: [RouterOutlet, Navbar],
  templateUrl: './Nimbus.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './Nimbus.css',
})
export class Nimbus {}
