import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';

import { type PaxBriefingFormGroup } from '../../../../core/flight-preparations/flight-preparation-form';

/** Section 09 — passenger briefing text, falling back to the owner's default briefing template
 *  from settings (ofp-settings, issue #27) until the pilot customizes it for this flight. */
@Component({
  selector: 'Nimbus-pax-briefing-section',
  imports: [ReactiveFormsModule],
  templateUrl: './pax-briefing-section.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['./pax-briefing-section.css', '../section-fields.css'],
})
export class PaxBriefingSection {
  formGroup = input.required<PaxBriefingFormGroup>();
}
