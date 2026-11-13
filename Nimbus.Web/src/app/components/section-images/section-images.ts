import { ChangeDetectionStrategy, Component, effect, inject, input } from '@angular/core';

import { ACCEPTED_IMAGE_TYPES, FlightImagesEditor } from '../../core/flight-preparations/flight-images.editor';

/**
 * The images attached to one section of a flight (issue #50): add (with progress and cancel), reorder, remove. It talks to
 * the API on its own, apart from the dossier form, so a failed upload never costs the pilot what they typed. A flight that
 * has not been saved yet has nothing to attach images to; `flightId` is null then and the block says so.
 */
@Component({
  selector: 'Nimbus-section-images',
  providers: [FlightImagesEditor],
  templateUrl: './section-images.html',
  styleUrl: './section-images.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SectionImages {
  /** The saved flight's id, or null while the flight has not been saved. */
  flightId = input<string | null>(null);
  /** The section the images are tagged with (`FlightPreparationSection`, e.g. `Meteo`). */
  section = input.required<string>();
  /** What the images are called in the UI, for labels. */
  noun = input('image');

  protected readonly editor = inject(FlightImagesEditor);
  protected readonly accept = ACCEPTED_IMAGE_TYPES.join(',');

  constructor() {
    effect(() => this.editor.bind(this.flightId(), this.section()));
  }

  protected picked(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.editor.add(Array.from(input.files ?? []));
    input.value = ''; // the same file can be picked again after a failure
  }

  protected size(bytes: number): string {
    return bytes >= 1024 * 1024 ? `${(bytes / (1024 * 1024)).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }
}
