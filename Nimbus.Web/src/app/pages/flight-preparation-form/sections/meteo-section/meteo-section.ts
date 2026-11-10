import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';

import { createWindLevelGroup, type MeteoFormGroup } from '../../../../core/flight-preparations/flight-preparation-form';
import { MetarTafFetcher } from '../../../../core/flight-preparations/metar-taf-fetcher';
import { FieldErrorPipe } from '../field-error.pipe';

/**
 * Section 02 — wind/visibility/METAR-TAF plus the wind-on-altitude table. The Fetch action is in this section's header
 * (the page owns it, see `MetarTafFetcher`); here the section shows how the last fetch went, or why none is possible.
 */
@Component({
  selector: 'Nimbus-meteo-section',
  imports: [ReactiveFormsModule, FieldErrorPipe],
  templateUrl: './meteo-section.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['./meteo-section.css', '../section-fields.css'],
})
export class MeteoSection {
  group = input.required<MeteoFormGroup>();
  private readonly fb = inject(NonNullableFormBuilder);
  protected readonly fetcher = inject(MetarTafFetcher);

  protected get windLevels() {
    return this.group().controls.windLevels;
  }

  protected addWindLevel(): void {
    this.windLevels.push(createWindLevelGroup(this.fb));
  }

  protected removeWindLevel(index: number): void {
    this.windLevels.removeAt(index);
  }
}
