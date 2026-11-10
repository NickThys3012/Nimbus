import { ChangeDetectionStrategy, Component, effect, input, signal } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { QuillEditorComponent } from 'ngx-quill';

import { Button } from '../../../../components/button/button';
import { RichTextView } from '../../../../components/rich-text-view/rich-text-view';
import { type PaxBriefingFormGroup } from '../../../../core/flight-preparations/flight-preparation-form';
import {
  defaultPaxBriefing,
  PAX_BRIEFING_TEMPLATE_HTML,
  PAX_BRIEFING_TEMPLATE_VERSION,
} from '../../../../core/flight-preparations/pax-briefing-template';

/** Only what the API keeps when it sanitises a stored briefing, so nothing typed here is silently dropped on save. */
const FORMATS = ['header', 'bold', 'italic', 'underline', 'strike', 'list', 'blockquote', 'link'];
const MODULES = {
  toolbar: [
    [{ header: [2, 3, false] }],
    ['bold', 'italic', 'underline', 'strike'],
    [{ list: 'ordered' }, { list: 'bullet' }],
    ['blockquote', 'link'],
    ['clean'],
  ],
};

/**
 * Section 09 — the passenger briefing as rich text in one HTML field. A new flight starts with the
 * standard briefing; editing it marks the flight's briefing as customized, and "Reset to template"
 * restores the default without touching any other section. The API sanitises the HTML on save.
 */
@Component({
  selector: 'Nimbus-pax-briefing-section',
  imports: [ReactiveFormsModule, QuillEditorComponent, RichTextView, Button],
  templateUrl: './pax-briefing-section.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['./pax-briefing-section.css', '../section-fields.css'],
})
export class PaxBriefingSection {
  group = input.required<PaxBriefingFormGroup>();

  protected readonly formats = FORMATS;
  protected readonly modules = MODULES;
  protected readonly templateVersion = PAX_BRIEFING_TEMPLATE_VERSION;

  /** Mirrors the form's "customized" flag so the OnPush template re-renders when it flips. */
  protected readonly custom = signal(false);
  protected readonly text = signal('');

  constructor() {
    effect((onCleanup) => {
      const group = this.group();
      this.custom.set(group.controls.paxBriefingIsCustom.value);
      this.text.set(group.controls.paxBriefingText.value);
      const sub = group.controls.paxBriefingText.valueChanges.subscribe((value) => {
        // Customized means "differs from the standard wording"; no explicit tick to forget.
        const custom = value !== PAX_BRIEFING_TEMPLATE_HTML;
        this.text.set(value);
        this.custom.set(custom);
        group.controls.paxBriefingIsCustom.setValue(custom, { emitEvent: false });
        if (!custom) group.controls.paxBriefingVersion.setValue(PAX_BRIEFING_TEMPLATE_VERSION, { emitEvent: false });
      });
      onCleanup(() => sub.unsubscribe());
    });
  }

  /** Restores the standard briefing. Only this section's three fields change; the rest of the form is untouched. */
  protected reset(): void {
    const group = this.group();
    group.setValue(defaultPaxBriefing());
    group.markAsDirty();
    this.custom.set(false);
    this.text.set(PAX_BRIEFING_TEMPLATE_HTML);
  }
}
