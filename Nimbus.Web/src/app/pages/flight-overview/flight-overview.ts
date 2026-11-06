import { Component, ChangeDetectionStrategy } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'Nimbus-flight-overview',
  imports: [RouterLink],
  templateUrl: './flight-overview.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './flight-overview.css',
})
export default class FlightOverview {}
