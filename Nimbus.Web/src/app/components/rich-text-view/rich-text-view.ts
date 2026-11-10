import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Read-only display of rich text (HTML) the editor produced. The HTML is bound with a plain
 * `[innerHTML]`, so Angular's own sanitiser runs on every render: a script tag or an event handler
 * in the stored value cannot execute here, whatever the server stored. (`quill-view-html` is not
 * used on purpose: it bypasses Angular's sanitiser unless told otherwise.) The Quill classes only
 * borrow the editor's typography, so what is shown matches what was typed.
 */
@Component({
  selector: 'Nimbus-rich-text-view',
  templateUrl: './rich-text-view.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './rich-text-view.css',
})
export class RichTextView {
  readonly html = input<string | null | undefined>('');
}
